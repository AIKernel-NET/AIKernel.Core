namespace AIKernel.Kernel.Inference;

/// <summary>Execution contract for Qwen3 dense; intentionally separate from GGUF storage admission.</summary>
internal sealed record Qwen3ModelDefinition(int Width,int Hidden,int Heads,int KvHeads,int HeadSize,int LayerCount,float Epsilon);
