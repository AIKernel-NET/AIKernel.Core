namespace AIKernel.Kernel.Inference;

/// <summary>Predicts immutable pages from dense layer order and this request's observed expert dispatches.</summary>
internal sealed class WeightPageHints
{
    private readonly PageReadAhead reader;
    private readonly Func<long, long> coldPage;
    private readonly int depth;
    private readonly Dictionary<int, int[]> selections = new();
    private int[] previous = [];
    private long epoch;
    private bool pressure;
    internal WeightPageHints(PageReadAhead reader, int depth, Func<long, long> coldPage)
        => (this.reader, this.depth, this.coldPage) = (reader, depth, coldPage);

    internal void Dense(DenseLayer[] layers, int index, CancellationToken token)
    {
        Next();
        for (int offset = 1; offset <= Math.Min(depth, layers.Length - index - 1); offset++)
        {
            var layer = layers[index + offset];
            Offer(layer.Q.Backing, epoch + offset, token);
            Offer(layer.K.Backing, epoch + offset, token);
        }
    }

    internal void Experts(int index, long start, long stride, CancellationToken token)
    {
        Next();
        var candidates = selections.GetValueOrDefault(index, previous);
        for (int i = 0; i < Math.Min(depth, candidates.Length); i++)
            Offer(start + stride * candidates[i], epoch, token, candidates[i]);
    }

    internal void Selected(int index, ReadOnlySpan<int> order)
    {
        reader.RejectUnselected(epoch, order);
        if (!selections.TryGetValue(index, out var target)) selections[index] = target = new int[order.Length];
        order.CopyTo(target);
        previous = target;
    }

    private void Next() => reader.Expire(++epoch);

    private void Offer(long backing, long expires, CancellationToken token, int expert = -1)
    {
        if (pressure) return;
        long page = coldPage(backing);
        if (page >= 0) reader.Offer(page, expires, token, expert);
    }

    internal void SetPressure(bool limited)
    {
        if (limited && !pressure) reader.Relinquish();
        pressure = limited;
    }

    internal void EndRequest()
    {
        reader.Cancel();
        selections.Clear();
        previous = [];
    }
}
