using System.Text.Json;

namespace AIKernel.Kernel.Inference;

internal static class SnapshotTokenizer
{
    internal static QwenTokenizer Create(SnapshotFiles files)
    {
        using var tokenizer = files.Json("tokenizer.json");
        using var config = files.Json("config.json");
        var root = tokenizer.RootElement;
        return new QwenTokenizer(Bpe(root), config.RootElement.GetProperty("eos_token_id").GetInt32());
    }
    private static GgufByteBpe Bpe(JsonElement root)
    {
        var model = root.GetProperty("model");
        if (model.GetProperty("type").GetString() != "BPE") throw new NotSupportedException("Expected byte BPE.");
        var table = Vocabulary(root, model);
        var merges = model.GetProperty("merges").EnumerateArray().Select(Merge).ToArray();
        return new GgufByteBpe(table, merges);
    }
    private static string[] Vocabulary(JsonElement root, JsonElement model)
    {
        var vocab = new Dictionary<int,string>();
        foreach(var item in model.GetProperty("vocab").EnumerateObject()) vocab.Add(item.Value.GetInt32(),item.Name);
        foreach (var item in root.GetProperty("added_tokens").EnumerateArray())
            vocab[item.GetProperty("id").GetInt32()] = item.GetProperty("content").GetString()!;
        var table = new string[vocab.Count];
        for (int id=0;id<table.Length;id++) table[id]=vocab[id];
        return table;
    }
    private static string Merge(JsonElement pair) => pair.ValueKind == JsonValueKind.String ? pair.GetString()!
        : pair[0].GetString() + " " + pair[1].GetString();
}
