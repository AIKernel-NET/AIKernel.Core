using AIKernel.Kernel.Inference;

namespace AIKernel.Core.Tests.Inference;

public sealed class InferenceCleanupTests
{
    [Fact]
    public void BothFailuresArePreservedAndInstanceIsInvalidated()
    {
        var inference=new OperationCanceledException();var wipe=new InvalidOperationException("driver failure");
        bool invalidated=false;int calls=0;
        var error=Assert.Throws<InferenceCleanupException>(()=>InferenceRequestCleanup.Execute<int>(()=>throw inference,
            ()=>{calls++;throw wipe;},()=>invalidated=true));
        Assert.Same(inference,error.InferenceFailure);Assert.Same(wipe,error.CleanupFailure);
        Assert.Equal(2,Assert.IsType<AggregateException>(error.InnerException).InnerExceptions.Count);
        Assert.True(invalidated);Assert.Equal(1,calls);
    }
    [Fact]
    public void ACompletedResultCannotEscapeWhenWipeFails()
    {
        bool invalidated=false;
        var error=Assert.Throws<InferenceCleanupException>(()=>InferenceRequestCleanup.Execute(()=>42,
            ()=>throw new InvalidOperationException(),()=>invalidated=true));
        Assert.Null(error.InferenceFailure);Assert.True(invalidated);
    }
    [Fact]
    public void SuccessfulCleanupRetainsOriginalCancellation()
    {
        var cancellation=new OperationCanceledException();bool cleared=false;
        var error=Assert.Throws<OperationCanceledException>(()=>InferenceRequestCleanup.Execute<int>(()=>throw cancellation,
            ()=>cleared=true,()=>throw new Exception("Unexpected invalidation")));
        Assert.Same(cancellation,error);Assert.True(cleared);
    }
}
