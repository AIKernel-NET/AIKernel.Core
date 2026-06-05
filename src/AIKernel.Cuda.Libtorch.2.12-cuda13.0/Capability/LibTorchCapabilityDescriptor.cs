namespace AIKernel.Cuda.Libtorch.Cuda13.Capability;

using AIKernel.Dtos.Capabilities;
using AIKernel.Enums;

public static class LibTorchCapabilityDescriptor
{
    public const string CapabilityId = "libtorch.llama.cuda13.0";
    public const string Name = "LibTorch Llama CUDA 13.0";
    public const string Version = "2.12.0";

    public static CapabilityModuleDescriptor Create()
    {
        return new CapabilityModuleDescriptor(
            CapabilityId: CapabilityId,
            Name: Name,
            Kind: CapabilityModuleKind.NativeLibrary,
            InvocationMode: CapabilityInvocationMode.NativeAbi,
            Version: Version,
            EntryPoint: "libtorch_bridge",
            ArtifactUri: null,
            ArtifactHash: null,
            ProvidedOperations: ["load_model", "unload_model", "forward"],
            RequiredPermissions:
            [
                "native-abi",
                "gpu.cuda",
                "filesystem.read"
            ],
            Metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["abi.calling_convention"] = "cdecl",
                ["abi.library"] = "libtorch_bridge",
                ["libtorch.version"] = "2.12.0",
                ["cuda.version"] = "13.0",
                ["runtime.win-x64"] = "Runtime/win-x64/libtorch",
                ["runtime.env"] = "AIKERNEL_LIBTORCH_PATH"
            });
    }
}
