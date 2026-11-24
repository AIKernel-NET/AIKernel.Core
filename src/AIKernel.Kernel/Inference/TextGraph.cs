namespace AIKernel.Kernel.Inference;

/// <summary>Layer scheduling for dense and routed graphs. Immutable weights can remain resident across requests.</summary>
internal sealed class TextGraph(int layerCount, RoutingPlan? routing = null)
{
    internal RoutingPlan? Routing { get; } = routing;

    internal IEnumerable<int> Layers(CancellationToken stop)
    {
        for (int i = 0; i < layerCount; i++)
        {
            stop.ThrowIfCancellationRequested();
            yield return i;
        }
    }
}
