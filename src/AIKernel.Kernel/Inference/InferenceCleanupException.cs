namespace AIKernel.Kernel.Inference;

/// <summary>Scratch clearing failed. The affected model rejects further requests; destroying its context does not prove zeroing.</summary>
public sealed class InferenceCleanupException : InvalidOperationException
{
    /// <summary>The earlier inference failure, if inference also failed.</summary>
    public Exception? InferenceFailure {get;}
    /// <summary>The cleanup failure that prevented confirmation of scratch clearing.</summary>
    public Exception CleanupFailure {get;}
    internal InferenceCleanupException(Exception? inference,Exception cleanup)
        :base("GPU scratch clearing could not be confirmed; this model instance cannot accept another request.",
            inference is null?cleanup:new AggregateException(inference,cleanup))
    {InferenceFailure=inference;CleanupFailure=cleanup;}
}

internal static class InferenceRequestCleanup
{
    internal static T Execute<T>(Func<T> work,Action cleanup,Action invalidate)
    {
        Exception? failure=null;
        try{return work();}
        catch(Exception error){failure=error;throw;}
        finally
        {
            try{cleanup();}
            catch(Exception error){invalidate();throw new InferenceCleanupException(failure,error);}
        }
    }
}
