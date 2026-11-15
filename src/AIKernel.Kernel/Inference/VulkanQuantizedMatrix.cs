using System.Diagnostics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;

namespace AIKernel.Kernel.Inference;

/// <summary>A compute-capable Vulkan physical device. Ordinals are Vulkan ordinals, not CUDA ordinals.</summary>
public sealed record VulkanComputeDeviceInfo(int Ordinal, string Name, uint VendorId, uint DeviceId,
    uint ApiVersion, bool Integrated);

// Uses only Vulkan 1.1 core compute. Coherent mapped allocations support integrated GPUs
// without staging a second model-sized CPU copy. No window, surface or presentation queue.
internal sealed unsafe class VulkanQuantizedMatrix : IMatrixOffload
{
    private readonly Vk vk;
    private Instance instance;
    private PhysicalDevice physical;
    private Device device;
    private Queue queue;
    private PhysicalDeviceProperties properties;
    private PhysicalDeviceMemoryProperties memory;
    private DescriptorSetLayout setLayout;
    private DescriptorPool descriptorPool;
    private DescriptorSet set;
    private PipelineLayout pipelineLayout;
    private Pipeline pipeline;
    private CommandPool commandPool;
    private CommandBuffer command;
    private Fence fence;
    private QueryPool timingQueries;
    private uint timestampBits;
    private bool subgroup;
    private readonly bool profile=Environment.GetEnvironmentVariable("AIKERNEL_VULKAN_PROFILE")=="1";
    private long calls, cpuTicks, waitTicks;
    private double gpuNanoseconds;
    internal bool DiagnosticReadOnly {get;set;}
    internal object Diagnostics=>new {calls,cpuMilliseconds=cpuTicks*1000.0/Stopwatch.Frequency,
        waitMilliseconds=waitTicks*1000.0/Stopwatch.Frequency,gpuMilliseconds=timingQueries.Handle==0?(double?)null:gpuNanoseconds/1e6,
        timestampBits,subgroup,weightMemoryFlags=weights?.Flags.ToString()};
    private Allocation? weights, input, output;
    private bool disposed;
    private readonly object sync = new();
    public long WeightBytes { get; }
    public string? PciBusId => null;
    internal VulkanComputeDeviceInfo DeviceInfo { get; private set; } = null!;
    private sealed class Allocation
    {
        internal Buffer Buffer;
        internal DeviceMemory Memory;
        internal void* Mapped;
        internal ulong Size;
        internal MemoryPropertyFlags Flags;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct Shape { internal uint Columns, Rows, Type, OffsetBytes, GroupWidth; }

    internal static IReadOnlyList<VulkanComputeDeviceInfo> Enumerate()
    {
        using var api = Vk.GetApi();
        Instance inst = CreateInstance(api);
        try
        {
            var list = new List<VulkanComputeDeviceInfo>();
            var devices = Devices(api, inst);
            for (int i = 0; i < devices.Length; i++)
                if (ComputeQueue(api, devices[i]) is not null)
                {
                    api.GetPhysicalDeviceProperties(devices[i], out var prop);
                    list.Add(Describe(i, prop));
                }
            return list.AsReadOnly();
        }
        finally { api.DestroyInstance(inst, null); }
    }

    internal VulkanQuantizedMatrix(GgufWeights file, int ordinal, long maximumWeightBytes)
    {
        if (file.Length <= 0 || file.Length > maximumWeightBytes || file.Length > int.MaxValue)
            throw new ArgumentException("Encoded model exceeds the Vulkan weight budget.");
        vk = Vk.GetApi();
        WeightBytes = file.Length;
        try
        {
            instance = CreateInstance(vk);
            var devices = Devices(vk, instance);
            if ((uint)ordinal >= devices.Length) throw new ArgumentOutOfRangeException(nameof(ordinal));
            physical = devices[ordinal];
            vk.GetPhysicalDeviceProperties(physical, out properties);
            vk.GetPhysicalDeviceMemoryProperties(physical, out memory);
            var sub=new PhysicalDeviceSubgroupProperties{SType=StructureType.PhysicalDeviceSubgroupProperties};
            var props2=new PhysicalDeviceProperties2{SType=StructureType.PhysicalDeviceProperties2,PNext=&sub};
            vk.GetPhysicalDeviceProperties2(physical,&props2);
            bool supported=(sub.SupportedStages&ShaderStageFlags.ComputeBit)!=0&&(sub.SupportedOperations&SubgroupFeatureFlags.ArithmeticBit)!=0;
            string? requested=Environment.GetEnvironmentVariable("AIKERNEL_VULKAN_KERNEL");
            if(requested is not (null or "portable" or "subgroup"))throw new ArgumentException("Unknown Vulkan kernel.");
            if(requested=="subgroup"&&!supported)throw new PlatformNotSupportedException("Compute subgroup arithmetic is unavailable.");
            subgroup=supported&&requested!="portable";
            DeviceInfo = Describe(ordinal, properties);
            if (properties.ApiVersion < Vk.Version11 || properties.Limits.MaxComputeWorkGroupInvocations < 128 ||
                properties.Limits.MaxComputeWorkGroupSize[0] < 128)
                throw new PlatformNotSupportedException("Vulkan 1.1 with 128-lane compute groups is required.");
            uint family = ComputeQueue(vk, physical) ?? throw new PlatformNotSupportedException("No Vulkan compute queue.");
            if(profile)
            {
                uint count=0;vk.GetPhysicalDeviceQueueFamilyProperties(physical,&count,null);
                var families=new QueueFamilyProperties[count];
                fixed(QueueFamilyProperties* fp=families)vk.GetPhysicalDeviceQueueFamilyProperties(physical,&count,fp);
                timestampBits=families[family].TimestampValidBits;
            }
            float priority = 1;
            var queueInfo = new DeviceQueueCreateInfo { SType=StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex=family, QueueCount=1, PQueuePriorities=&priority };
            var create = new DeviceCreateInfo { SType=StructureType.DeviceCreateInfo, QueueCreateInfoCount=1, PQueueCreateInfos=&queueInfo };
            Check(vk.CreateDevice(physical, &create, null, out device), "create device");
            vk.GetDeviceQueue(device, family, 0, out queue);
            weights = Allocate(checked(((ulong)file.Length+3)&~3UL));
            new Span<byte>(weights.Mapped, checked((int)weights.Size)).Clear();
            const int chunk = 1024 * 1024;
            for (long offset=0; offset<file.Length; offset+=chunk)
            {
                int count=(int)Math.Min(chunk,file.Length-offset);
                System.Buffer.MemoryCopy(file.BytesAt(offset,count),(byte*)weights.Mapped+offset,count,count);
            }
            CreatePipeline(family);
            if(profile&&timestampBits>0)
            {
                var qi=new QueryPoolCreateInfo{SType=StructureType.QueryPoolCreateInfo,QueryType=QueryType.Timestamp,QueryCount=2};
                Check(vk.CreateQueryPool(device,&qi,null,out timingQueries),"create timestamp queries");
            }
        }
        catch { Dispose(); throw; }
    }

    private static Instance CreateInstance(Vk api)
    {
        var app = new ApplicationInfo { SType=StructureType.ApplicationInfo, ApiVersion=Vk.Version11 };
        var create = new InstanceCreateInfo { SType=StructureType.InstanceCreateInfo, PApplicationInfo=&app };
        Check(api.CreateInstance(&create,null,out var result),"create instance");
        return result;
    }
    private static PhysicalDevice[] Devices(Vk api,Instance inst)
    {
        uint count=0; Check(api.EnumeratePhysicalDevices(inst,&count,null),"enumerate device count");
        if(count>256)throw new InvalidOperationException("Unexpected Vulkan device count.");
        var result=new PhysicalDevice[count];
        fixed(PhysicalDevice* p=result)Check(api.EnumeratePhysicalDevices(inst,&count,p),"enumerate devices");
        return result;
    }
    private static uint? ComputeQueue(Vk api,PhysicalDevice physical)
    {
        uint count=0;api.GetPhysicalDeviceQueueFamilyProperties(physical,&count,null);
        if(count>256)throw new InvalidOperationException("Unexpected Vulkan queue count.");
        var families=new QueueFamilyProperties[count];
        fixed(QueueFamilyProperties* p=families)api.GetPhysicalDeviceQueueFamilyProperties(physical,&count,p);
        for(uint i=0;i<count;i++)if(families[i].QueueCount>0&&(families[i].QueueFlags&QueueFlags.ComputeBit)!=0)return i;
        return null;
    }
    private static VulkanComputeDeviceInfo Describe(int ordinal,PhysicalDeviceProperties p)
        =>new(ordinal,Marshal.PtrToStringUTF8((nint)p.DeviceName)??"Vulkan device",p.VendorID,p.DeviceID,p.ApiVersion,
            p.DeviceType==PhysicalDeviceType.IntegratedGpu);

    private Allocation Allocate(ulong size)
    {
        if(size==0||size>int.MaxValue)throw new ArgumentOutOfRangeException(nameof(size));
        var a=new Allocation {Size=size};
        try
        {
            var create=new BufferCreateInfo {SType=StructureType.BufferCreateInfo,Size=size,
                Usage=BufferUsageFlags.StorageBufferBit,SharingMode=SharingMode.Exclusive};
            Check(vk.CreateBuffer(device,&create,null,out a.Buffer),"create buffer");
            vk.GetBufferMemoryRequirements(device,a.Buffer,out var requirements);
            uint? selected=null;
            var required=MemoryPropertyFlags.HostVisibleBit|MemoryPropertyFlags.HostCoherentBit;
            for(uint i=0;i<memory.MemoryTypeCount;i++)
                if((requirements.MemoryTypeBits&(1u<<(int)i))!=0&&(memory.MemoryTypes[(int)i].PropertyFlags&required)==required)
                {
                    selected=i;
                    if((memory.MemoryTypes[(int)i].PropertyFlags&MemoryPropertyFlags.DeviceLocalBit)!=0)break;
                }
            if(selected is null)throw new PlatformNotSupportedException("Vulkan coherent host-visible storage memory is required.");
            a.Flags=memory.MemoryTypes[(int)selected.Value].PropertyFlags;
            var info=new MemoryAllocateInfo {SType=StructureType.MemoryAllocateInfo,AllocationSize=requirements.Size,MemoryTypeIndex=selected.Value};
            Check(vk.AllocateMemory(device,&info,null,out a.Memory),"allocate memory");
            Check(vk.BindBufferMemory(device,a.Buffer,a.Memory,0),"bind buffer");
            void* mapped=null;Check(vk.MapMemory(device,a.Memory,0,size,0,&mapped),"map memory");a.Mapped=mapped;
            return a;
        }
        catch{Free(a);throw;}
    }
    private void Free(Allocation? a)
    {
        if(a is null)return;
        if(a.Mapped!=null){vk.UnmapMemory(device,a.Memory);a.Mapped=null;}
        if(a.Buffer.Handle!=0){vk.DestroyBuffer(device,a.Buffer,null);a.Buffer=default;}
        if(a.Memory.Handle!=0){vk.FreeMemory(device,a.Memory,null);a.Memory=default;}
    }
    private void EnsureScratch(ref Allocation? a,ulong size)
    {
        // Request dimensions are bounded by the model parser; retain at most 8 MiB per scratch buffer.
        if(size>8*1024*1024)throw new ArgumentException("Vulkan scratch dimensions exceed the bound.");
        if(a is not null&&a.Size>=size)return;
        var next=Allocate(size);Free(a);a=next;
    }
    private void CreatePipeline(uint family)
    {
        var bindings=stackalloc DescriptorSetLayoutBinding[3];
        for(uint i=0;i<3;i++)bindings[i]=new(){Binding=i,DescriptorType=DescriptorType.StorageBuffer,DescriptorCount=1,StageFlags=ShaderStageFlags.ComputeBit};
        var setInfo=new DescriptorSetLayoutCreateInfo{SType=StructureType.DescriptorSetLayoutCreateInfo,BindingCount=3,PBindings=bindings};
        Check(vk.CreateDescriptorSetLayout(device,&setInfo,null,out setLayout),"create descriptor layout");
        var push=new PushConstantRange{StageFlags=ShaderStageFlags.ComputeBit,Size=(uint)sizeof(Shape)};
        var layout=setLayout;
        var layoutInfo=new PipelineLayoutCreateInfo{SType=StructureType.PipelineLayoutCreateInfo,SetLayoutCount=1,PSetLayouts=&layout,PushConstantRangeCount=1,PPushConstantRanges=&push};
        Check(vk.CreatePipelineLayout(device,&layoutInfo,null,out pipelineLayout),"create pipeline layout");
        using var stream=typeof(VulkanQuantizedMatrix).Assembly.GetManifestResourceStream("AIKernel.Kernel.Inference.Shaders."+(subgroup?"quantized_gemv_subgroup.spv":"quantized_gemv.spv"))
            ??throw new InvalidOperationException("Missing embedded AIKernel Vulkan shader.");
        byte[] code=new byte[checked((int)stream.Length)];stream.ReadExactly(code);
        ShaderModule shader=default;
        try
        {
            fixed(byte* bytes=code)
            {
                var shaderInfo=new ShaderModuleCreateInfo{SType=StructureType.ShaderModuleCreateInfo,CodeSize=(nuint)code.Length,PCode=(uint*)bytes};
                Check(vk.CreateShaderModule(device,&shaderInfo,null,out shader),"create shader");
            }
            byte* name=stackalloc byte[]{(byte)'m',(byte)'a',(byte)'i',(byte)'n',0};
            var stage=new PipelineShaderStageCreateInfo{SType=StructureType.PipelineShaderStageCreateInfo,Stage=ShaderStageFlags.ComputeBit,Module=shader,PName=name};
            var info=new ComputePipelineCreateInfo{SType=StructureType.ComputePipelineCreateInfo,Stage=stage,Layout=pipelineLayout};
            Check(vk.CreateComputePipelines(device,default,1,&info,null,out pipeline),"create compute pipeline");
        }
        finally{if(shader.Handle!=0)vk.DestroyShaderModule(device,shader,null);}
        var poolSize=new DescriptorPoolSize{Type=DescriptorType.StorageBuffer,DescriptorCount=3};
        var poolInfo=new DescriptorPoolCreateInfo{SType=StructureType.DescriptorPoolCreateInfo,MaxSets=1,PoolSizeCount=1,PPoolSizes=&poolSize};
        Check(vk.CreateDescriptorPool(device,&poolInfo,null,out descriptorPool),"create descriptor pool");
        var alloc=new DescriptorSetAllocateInfo{SType=StructureType.DescriptorSetAllocateInfo,DescriptorPool=descriptorPool,DescriptorSetCount=1,PSetLayouts=&layout};
        Check(vk.AllocateDescriptorSets(device,&alloc,out set),"allocate descriptor set");
        var commandInfo=new CommandPoolCreateInfo{SType=StructureType.CommandPoolCreateInfo,QueueFamilyIndex=family,Flags=CommandPoolCreateFlags.ResetCommandBufferBit};
        Check(vk.CreateCommandPool(device,&commandInfo,null,out commandPool),"create command pool");
        var commandAlloc=new CommandBufferAllocateInfo{SType=StructureType.CommandBufferAllocateInfo,CommandPool=commandPool,Level=CommandBufferLevel.Primary,CommandBufferCount=1};
        Check(vk.AllocateCommandBuffers(device,&commandAlloc,out command),"allocate command buffer");
        var fenceInfo=new FenceCreateInfo{SType=StructureType.FenceCreateInfo};Check(vk.CreateFence(device,&fenceInfo,null,out fence),"create fence");
    }

    public void Multiply(GgufWeights.Tensor tensor,float[] x,float[] y,CancellationToken token,InferenceProfiler? inferenceProfile=null,PreparedQ8Activation? prepared=null)
    {
        token.ThrowIfCancellationRequested();
        lock(sync)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            long started=profile?Stopwatch.GetTimestamp():0;
            long size=checked((long)tensor.Rows*tensor.RowBytes);
            if(tensor.Rows<1||tensor.Columns<1||x.Length<tensor.Columns||y.Length<tensor.Rows||tensor.Offset<0||
                tensor.Offset>WeightBytes||size>WeightBytes-tensor.Offset||tensor.Type is not (0 or 41)||
                tensor.Type==41&&tensor.Columns%128!=0)
                throw new ArgumentException("Invalid Vulkan matrix dimensions, encoding or mapped range.");
            ulong alignment=Math.Max(4,properties.Limits.MinStorageBufferOffsetAlignment);
            ulong offset=(ulong)tensor.Offset/alignment*alignment;
            ulong range=((ulong)tensor.Offset-offset+(ulong)size+3)&~3UL;
            if(range>properties.Limits.MaxStorageBufferRange)throw new NotSupportedException("Matrix exceeds this Vulkan device's storage-buffer range.");
            uint groupWidth=Math.Min((uint)tensor.Rows,properties.Limits.MaxComputeWorkGroupCount[0]);
            uint groupHeight=((uint)tensor.Rows+groupWidth-1)/groupWidth;
            if(groupHeight>properties.Limits.MaxComputeWorkGroupCount[1])throw new NotSupportedException("Matrix exceeds compute dispatch limits.");
            EnsureScratch(ref input,(ulong)tensor.Columns*4);EnsureScratch(ref output,(ulong)tensor.Rows*4);
            x.AsSpan(0,tensor.Columns).CopyTo(new Span<float>(input!.Mapped,tensor.Columns));
            var buffers=stackalloc DescriptorBufferInfo[3];
            buffers[0]=new(){Buffer=weights!.Buffer,Offset=offset,Range=range};
            buffers[1]=new(){Buffer=input.Buffer,Range=(ulong)tensor.Columns*4};
            buffers[2]=new(){Buffer=output!.Buffer,Range=(ulong)tensor.Rows*4};
            var writes=stackalloc WriteDescriptorSet[3];
            for(uint i=0;i<3;i++)writes[i]=new(){SType=StructureType.WriteDescriptorSet,DstSet=set,DstBinding=i,DescriptorCount=1,DescriptorType=DescriptorType.StorageBuffer,PBufferInfo=buffers+i};
            vk.UpdateDescriptorSets(device,3,writes,0,null);
            Check(vk.ResetCommandBuffer(command,0),"reset command");
            var begin=new CommandBufferBeginInfo{SType=StructureType.CommandBufferBeginInfo,Flags=CommandBufferUsageFlags.OneTimeSubmitBit};
            Check(vk.BeginCommandBuffer(command,&begin),"begin command");
            if(timingQueries.Handle!=0)
            {
                vk.CmdResetQueryPool(command,timingQueries,0,2);
                vk.CmdWriteTimestamp(command,PipelineStageFlags.TopOfPipeBit,timingQueries,0);
            }
            vk.CmdBindPipeline(command,PipelineBindPoint.Compute,pipeline);
            var descriptor=set;vk.CmdBindDescriptorSets(command,PipelineBindPoint.Compute,pipelineLayout,0,1,&descriptor,0,null);
            var shape=new Shape{Columns=(uint)tensor.Columns,Rows=(uint)tensor.Rows,Type=DiagnosticReadOnly&&tensor.Type==41?42u:(uint)tensor.Type,OffsetBytes=(uint)((ulong)tensor.Offset-offset),GroupWidth=groupWidth};
            vk.CmdPushConstants(command,pipelineLayout,ShaderStageFlags.ComputeBit,0,(uint)sizeof(Shape),&shape);
            vk.CmdDispatch(command,groupWidth,groupHeight,1);
            if(timingQueries.Handle!=0)vk.CmdWriteTimestamp(command,PipelineStageFlags.BottomOfPipeBit,timingQueries,1);
            var barrier=new MemoryBarrier{SType=StructureType.MemoryBarrier,SrcAccessMask=AccessFlags.ShaderWriteBit,DstAccessMask=AccessFlags.HostReadBit};
            vk.CmdPipelineBarrier(command,PipelineStageFlags.ComputeShaderBit,PipelineStageFlags.HostBit,0,1,&barrier,0,null,0,null);
            Check(vk.EndCommandBuffer(command),"end command");
            var f=fence;Check(vk.ResetFences(device,1,&f),"reset fence");
            var cmd=command;var submit=new SubmitInfo{SType=StructureType.SubmitInfo,CommandBufferCount=1,PCommandBuffers=&cmd};
            long waiting=profile?Stopwatch.GetTimestamp():0;
            Check(vk.QueueSubmit(queue,1,&submit,fence),"submit compute");
            Check(vk.WaitForFences(device,1,&f,true,ulong.MaxValue),"wait for compute");
            if(profile)waitTicks+=Stopwatch.GetTimestamp()-waiting;
            new ReadOnlySpan<float>(output.Mapped,tensor.Rows).CopyTo(y);
            if(profile)
            {
                calls++;cpuTicks+=Stopwatch.GetTimestamp()-started;
                if(timingQueries.Handle!=0)
                {
                    ulong* ticks=stackalloc ulong[2];
                    Check(vk.GetQueryPoolResults(device,timingQueries,0,2,16,ticks,8,(QueryResultFlags)3),"read timestamps");
                    ulong mask=timestampBits>=64?ulong.MaxValue:(1UL<<(int)timestampBits)-1;
                    gpuNanoseconds+=((ticks[1]-ticks[0])&mask)*properties.Limits.TimestampPeriod;
                }
            }
            token.ThrowIfCancellationRequested();
        }
    }
    public void ZeroScratch()
    {
        lock(sync)
        {
            if(disposed)return;
            if(input is not null)new Span<byte>(input.Mapped,(int)input.Size).Clear();
            if(output is not null)new Span<byte>(output.Mapped,(int)output.Size).Clear();
        }
    }
    public void Dispose()
    {
        lock(sync)
        {
            if(disposed)return;disposed=true;
            if(device.Handle!=0)
            {
                vk.DeviceWaitIdle(device);
                if(input is not null&&input.Mapped!=null)new Span<byte>(input.Mapped,(int)input.Size).Clear();
                if(output is not null&&output.Mapped!=null)new Span<byte>(output.Mapped,(int)output.Size).Clear();
                if(timingQueries.Handle!=0)vk.DestroyQueryPool(device,timingQueries,null);
                if(fence.Handle!=0)vk.DestroyFence(device,fence,null);
                if(commandPool.Handle!=0)vk.DestroyCommandPool(device,commandPool,null);
                if(descriptorPool.Handle!=0)vk.DestroyDescriptorPool(device,descriptorPool,null);
                if(pipeline.Handle!=0)vk.DestroyPipeline(device,pipeline,null);
                if(pipelineLayout.Handle!=0)vk.DestroyPipelineLayout(device,pipelineLayout,null);
                if(setLayout.Handle!=0)vk.DestroyDescriptorSetLayout(device,setLayout,null);
                Free(output);Free(input);Free(weights);vk.DestroyDevice(device,null);
            }
            if(instance.Handle!=0)vk.DestroyInstance(instance,null);
            vk.Dispose();
        }
    }
    private static void Check(Result result,string operation)
    {
        if(result!=Result.Success)throw new InvalidOperationException($"Vulkan {operation} failed: {result}.");
    }
}
