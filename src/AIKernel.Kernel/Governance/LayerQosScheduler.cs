using AIKernel.Common.Results;
using AIKernel.Core.Governance;

namespace AIKernel.Kernel.Governance;

/// <summary>Cooperatively grants one device to requests at synchronous layer boundaries.</summary>
public sealed class LayerQosScheduler
{
    private static readonly ErrorContext Cancelled = new("Device execution was cancelled.", "device_execution_cancelled", false)
        { FailureKind = FailureKind.FailClosed };
    private static readonly ErrorContext Full = new("The device execution queue is full.", "device_queue_full", true)
        { FailureKind = FailureKind.FailClosed };
    private static readonly ErrorContext InvalidSpill = new("The spill callback returned no error details.",
        "device_preemption_failed", false) { FailureKind = FailureKind.FailClosed };
    private readonly object gate = new();
    private readonly LinkedList<Lease>[] queues = [new(), new(), new(), new()];
    private readonly int capacity;
    private Lease? running;
    private ErrorContext? fault;
    private int admitted;

    /// <summary>Creates a device queue with bounded admission.</summary>
    public LayerQosScheduler(int capacity = 64)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        this.capacity = capacity;
    }

    /// <summary>The number of requests waiting for the device.</summary>
    public int WaitingCount
    {
        get { lock (gate) return admitted - (running is null ? 0 : 1); }
    }

    /// <summary>Waits for device ownership. The spill callback runs before ownership can change.</summary>
    public Result<Lease> Enter(InferencePriority priority, Func<Result<int>> spill, CancellationToken cancellationToken = default)
    {
        if ((uint)priority >= (uint)queues.Length || spill is null)
            return Result<Lease>.Fail(new ErrorContext("Invalid device execution request.", "device_queue_invalid", false)
                { FailureKind = FailureKind.FailClosed });

        lock (gate)
        {
            if (fault is not null) return Result<Lease>.Fail(fault);
            if (cancellationToken.IsCancellationRequested) return Result<Lease>.Fail(Cancelled);
            if (admitted == capacity) return Result<Lease>.Fail(Full);
            var lease = new Lease(this, priority, spill, cancellationToken);
            admitted++;
            var error = WaitForTurn(lease);
            if (error is null) return Result<Lease>.Success(lease);
            admitted--;
            return Result<Lease>.Fail(error);
        }
    }

    private ErrorContext? WaitForTurn(Lease lease)
    {
        queues[(int)lease.Priority].AddLast(lease.Node);
        try
        {
            while (true)
            {
                if (fault is not null) return fault;
                if (lease.Disposed || lease.Token.IsCancellationRequested) return Cancelled;
                if (running is null && ReferenceEquals(Head(), lease))
                {
                    running = lease;
                    return null;
                }
                Monitor.Wait(gate, 25);
            }
        }
        finally
        {
            lease.Node.List!.Remove(lease.Node);
            Monitor.PulseAll(gate);
        }
    }

    private Lease? Head()
    {
        for (var i = queues.Length - 1; i >= 0; i--)
            if (queues[i].First is { } node) return node.Value;
        return null;
    }

    private Result<bool> Checkpoint(Lease lease)
    {
        lock (gate)
        {
            if (fault is not null) return Result<bool>.Fail(fault);
            if (!ReferenceEquals(running, lease) || lease.Token.IsCancellationRequested)
                return Result<bool>.Fail(Cancelled);
            if (Head() is not { } next || next.Priority <= lease.Priority)
                return Result<bool>.Success(false);

            Result<int> spilled;
            try
            {
                spilled = lease.Spill();
            }
            catch (Exception ex)
            {
                spilled = Result<int>.Fail(new ErrorContext(ex.Message, "device_preemption_failed", false)
                    { FailureKind = FailureKind.FailClosed });
            }
            if (!spilled.IsSuccess)
            {
                fault = spilled.Error ?? InvalidSpill;
                Monitor.PulseAll(gate);
                return Result<bool>.Fail(fault);
            }

            running = null;
            lease.Preemptions++;
            var error = WaitForTurn(lease);
            return error is null ? Result<bool>.Success(true) : Result<bool>.Fail(error);
        }
    }

    private void Release(Lease lease)
    {
        lock (gate)
        {
            if (lease.Disposed) return;
            lease.Disposed = true;
            if (ReferenceEquals(running, lease)) running = null;
            admitted--;
            Monitor.PulseAll(gate);
        }
    }

    /// <summary>Ownership of the scheduler slot, including time spent paused at a checkpoint.</summary>
    public sealed class Lease : IDisposable
    {
        private readonly LayerQosScheduler owner;

        internal Lease(LayerQosScheduler owner, InferencePriority priority, Func<Result<int>> spill, CancellationToken token)
        {
            this.owner = owner;
            Priority = priority;
            Spill = spill;
            Token = token;
            Node = new LinkedListNode<Lease>(this);
        }

        internal InferencePriority Priority { get; }
        internal Func<Result<int>> Spill { get; }
        internal CancellationToken Token { get; }
        internal LinkedListNode<Lease> Node { get; }
        internal bool Disposed { get; set; }

        /// <summary>Counts times another request took the device at a layer boundary.</summary>
        public int Preemptions { get; internal set; }

        /// <summary>Yields to a strictly higher priority waiter and returns after resuming.</summary>
        public Result<bool> Checkpoint() => owner.Checkpoint(this);

        /// <inheritdoc />
        public void Dispose() => owner.Release(this);
    }
}
