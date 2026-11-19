using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class VulkanReadbackTests
{
    [Fact]
    public void FiniteProjectionIsReturnedWithoutReplay()
    {
        float[] mapped = [float.MinValue, -0.0f, float.Epsilon, float.MaxValue];
        var result = new float[mapped.Length];
        int submissions = 0;
        Assert.False(VulkanQuantizedMatrix.ReadProjection(mapped, result, () => submissions++, TestContext.Current.CancellationToken));
        Assert.Equal(1, submissions);
        Assert.Equal(mapped.Select(BitConverter.SingleToInt32Bits), result.Select(BitConverter.SingleToInt32Bits));
    }

    [Theory]
    [InlineData(0, float.PositiveInfinity)]
    [InlineData(82, float.NaN)]
    [InlineData(128, float.NegativeInfinity)]
    public void TransientNonFiniteRowReplaysEntireProjection(int row, float invalid)
    {
        var mapped = new float[129];
        var result = new float[mapped.Length];
        int submissions = 0;
        void Submit()
        {
            Array.Fill(mapped, ++submissions == 1 ? -1f : 2f);
            if (submissions == 1) mapped[row] = invalid;
        }
        Assert.True(VulkanQuantizedMatrix.ReadProjection(mapped, result, Submit, TestContext.Current.CancellationToken));
        Assert.Equal(2, submissions);
        Assert.All(result, value => Assert.Equal(2f, value));
    }

    [Fact]
    public void PersistentNonFiniteProjectionFailsAfterOneReplay()
    {
        float[] mapped = [float.NaN];
        int submissions = 0;
        Assert.Throws<ArithmeticException>(() =>
            VulkanQuantizedMatrix.ReadProjection(mapped, new float[1], () => submissions++, TestContext.Current.CancellationToken));
        Assert.Equal(2, submissions);
    }

    [Fact]
    public void CancelledRequestDoesNotResubmit()
    {
        using var stop = new CancellationTokenSource();
        float[] mapped = [float.NaN];
        int submissions = 0;
        void Submit() { submissions++; stop.Cancel(); }
        Assert.Throws<OperationCanceledException>(() =>
            VulkanQuantizedMatrix.ReadProjection(mapped, new float[1], Submit, stop.Token));
        Assert.Equal(1, submissions);
    }
}
