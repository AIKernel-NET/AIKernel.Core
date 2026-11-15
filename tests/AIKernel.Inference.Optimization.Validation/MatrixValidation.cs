using AIKernel.Kernel.Inference;
using System.Buffers.Binary;
using System.Text.Json;

internal static class MatrixValidation
{
    internal static void Run(string model)
    {
        using var file=new GgufWeights(model);using var stream=File.OpenRead(model);
        var random=new Random(140015);int cases=0,values=0;double worst=0,worstBoundRatio=0;
        int workersLimit=int.TryParse(Environment.GetEnvironmentVariable("AIKERNEL_BENCH_CPU_WORKERS"),out int configured)?configured:4;
        var backends=new[]{"scalar","avx","avx2","avx512"}.Where(CpuInstructionSets.Available.Contains).ToArray();
        foreach(var tensor in file.Tensors.Values.Where(t=>t.Type==41).GroupBy(t=>t.Columns).Select(g=>g.First()))
        foreach(int pattern in new[]{0,1,2,3})
        {
            int skip=tensor.Rows>1?1:0;
            var sample=tensor with {Rows=Math.Min(65,tensor.Rows-skip),Offset=tensor.Offset+(long)skip*tensor.RowBytes};
            float[] x=Enumerable.Range(0,sample.Columns).Select(i=>pattern switch {
                0=>(float)(random.NextDouble()*2-1),1=>(i%2==0?1f:-1f),
                2=>(float)(random.NextDouble()*2-1)*(i%7==0?10000f:.0001f),_=>0f}).ToArray();
            var expected=new double[sample.Rows];var bound=new double[sample.Rows];
            for(int row=0;row<sample.Rows;row++)
            {
                var packed=new byte[sample.RowBytes];stream.Position=sample.Offset+(long)row*sample.RowBytes;stream.ReadExactly(packed);
                for(int i=0;i<sample.Columns;i++)
                {
                    int block=i/128;double scale=(float)BitConverter.UInt16BitsToHalf(BinaryPrimitives.ReadUInt16LittleEndian(packed.AsSpan(block*18)));
                    double product=scale*x[i]*(((packed[block*18+2+i%128/8]>>(i%8))&1)==0?-1:1);
                    expected[row]+=product;bound[row]+=Math.Abs(product);
                }
                bound[row]=2e-6*bound[row]+1e-6;
            }
            foreach(string backend in backends)foreach(int workers in new[]{1,workersLimit})
            {
                var actual=new float[sample.Rows];QuantizedCpuMatrix.Multiply(file,sample,x,actual,new(){MaxDegreeOfParallelism=workers},backend);
                for(int row=0;row<actual.Length;row++)
                {
                    double error=Math.Abs(actual[row]-expected[row]);worst=Math.Max(worst,error);worstBoundRatio=Math.Max(worstBoundRatio,error/bound[row]);values++;
                    if(!float.IsFinite(actual[row])||error>bound[row])throw new ArithmeticException($"Q1 oracle mismatch: {backend}, {sample.Columns}, {pattern}, {row}, error {error}, limit {bound[row]}");
                }
                cases++;
            }
        }
        Console.WriteLine(JsonSerializer.Serialize(new{kind="double-oracle",passed=true,cases,values,worstAbsolute=worst,worstBoundRatio,backends}));
    }
}
