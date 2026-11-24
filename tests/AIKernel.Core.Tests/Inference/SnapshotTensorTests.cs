using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using AIKernel.Kernel.Inference;
using Xunit;

namespace AIKernel.Core.Tests.Inference;

public sealed class SnapshotTensorTests
{
    [Theory]
    [InlineData("BF16", 4)]
    [InlineData("F32", 8)]
    public void ImportsBothPrecisionsIntoIdenticalRows(string type, int byteCount)
    {
        string json = JsonSerializer.Serialize(new { x = new { dtype=type, shape=new[]{1,2}, data_offsets=new[]{0,byteCount} } });
        var payload = new byte[byteCount];
        if (type == "BF16") { BinaryPrimitives.WriteUInt16LittleEndian(payload,0x3fc0); BinaryPrimitives.WriteUInt16LittleEndian(payload.AsSpan(2),0xc010); }
        else { BinaryPrimitives.WriteSingleLittleEndian(payload,1.5f); BinaryPrimitives.WriteSingleLittleEndian(payload.AsSpan(4),-2.25f); }
        using var file = Fixture(json,payload);
        var tensors = new Dictionary<string,TensorSlice>();
        SnapshotTensors.ReadHeader(file,tensors);
        var values = tensors["x"].Materialize();
        Assert.Equal(new[]{1.5f,-2.25f}, values);
        using var store = new SnapshotMatrixStore();
        var matrix=store.Add(tensors["x"]); store.Seal(null);
        var actual=new float[2];store.ReadRow(matrix,0,actual);
        Assert.Equal(values,actual);
        Assert.Throws<InvalidDataException>(()=>SnapshotTensors.ReadHeader(file,tensors));
    }

    [Theory]
    [InlineData(4,12)]
    [InlineData(-1,7)]
    [InlineData(0,4)]
    public void RejectsMissingOverlappingOrInvalidExtents(int start,int end)
    {
        string json = JsonSerializer.Serialize(new { x = new { dtype="F32", shape=new[]{2}, data_offsets=new[]{start,end} } });
        using var file=Fixture(json,new byte[8]);
        Assert.Throws<InvalidDataException>(()=>SnapshotTensors.ReadHeader(file,[]));
    }

    private static FileStream Fixture(string header,byte[] payload)
    {
        var bytes=Encoding.UTF8.GetBytes(header);
        var file=new FileStream(Path.Combine(Path.GetTempPath(),Path.GetRandomFileName()),FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None,4096,FileOptions.DeleteOnClose);
        Span<byte> length=stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(length,bytes.Length);
        file.Write(length);file.Write(bytes);file.Write(payload);file.Flush();file.Position=0;return file;
    }
}
