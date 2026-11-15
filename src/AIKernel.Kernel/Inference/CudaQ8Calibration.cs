namespace AIKernel.Kernel.Inference;

/// <summary>Startup-only synchronous-call timing evidence; no model inputs or outputs are retained.</summary>
public sealed record CudaQ8Calibration(string SelectedKernel,string Reason,double Dp4aMedianMilliseconds,
    double TensorCoreMedianMilliseconds,double MedianPairedRatio,IReadOnlyList<double> PairedRatios,int CallsPerRound);

internal static class CudaQ8Selection
{
    internal const int Rounds=9,CallsPerRound=8;
    internal static CudaQ8Calibration Choose(double[] dp4a,double[] tensor)
    {
        if(dp4a.Length!=Rounds||tensor.Length!=Rounds||dp4a.Concat(tensor).Any(v=>!double.IsFinite(v)||v<=0))
            throw new ArgumentException("Nine positive finite paired timing samples are required.");
        var ratios=dp4a.Zip(tensor,(d,t)=>t/d).ToArray();
        double Median(IEnumerable<double> values)=>values.Order().ElementAt(Rounds/2);
        // Transfer/scheduling noise dominates small GEMVs. A single fast median is
        // insufficient: require a 10% paired median gain and a win in 8/9 pairs.
        // Permit one scheduling outlier instead of letting a single preemption
        // override an otherwise repeatable advantage.
        bool win=Median(ratios)<.90&&ratios.Count(r=>r<1)>=Rounds-1;
        return new(win?"tensor-core-int8":"dp4a",win?"consistent-paired-advantage":"no-consistent-tensor-advantage",
            Median(dp4a),Median(tensor),Median(ratios),Array.AsReadOnly(ratios),CallsPerRound);
    }
}
