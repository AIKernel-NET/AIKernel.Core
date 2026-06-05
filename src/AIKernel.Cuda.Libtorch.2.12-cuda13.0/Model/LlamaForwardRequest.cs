namespace AIKernel.Cuda.Libtorch.Cuda13.Model;

using System.Globalization;

public sealed record LlamaForwardRequest(
    int ModelHandle,
    int[] InputIds)
{
    public static LlamaForwardRequestParseResult TryCreate(
        IReadOnlyDictionary<string, string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        if (!arguments.TryGetValue("model_handle", out var modelHandleText) ||
            !int.TryParse(
                modelHandleText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var modelHandle) ||
            modelHandle <= 0)
        {
            return LlamaForwardRequestParseResult.Fail(
                "Argument 'model_handle' must be a positive integer.");
        }

        if (!arguments.TryGetValue("input_ids", out var inputIdsText) ||
            string.IsNullOrWhiteSpace(inputIdsText))
        {
            return LlamaForwardRequestParseResult.Fail(
                "Argument 'input_ids' is required.");
        }

        var tokens = inputIdsText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var inputIds = new int[tokens.Length];

        for (var i = 0; i < tokens.Length; i++)
        {
            if (!int.TryParse(
                tokens[i],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out inputIds[i]) ||
                inputIds[i] < 0)
            {
                return LlamaForwardRequestParseResult.Fail(
                    "Argument 'input_ids' must contain non-negative integer token ids.");
            }
        }

        return LlamaForwardRequestParseResult.Success(
            new LlamaForwardRequest(modelHandle, inputIds));
    }
}

public sealed record LlamaForwardRequestParseResult(
    bool Succeeded,
    LlamaForwardRequest? Value,
    string? ErrorMessage)
{
    public static LlamaForwardRequestParseResult Success(
        LlamaForwardRequest value)
    {
        return new LlamaForwardRequestParseResult(true, value, null);
    }

    public static LlamaForwardRequestParseResult Fail(
        string errorMessage)
    {
        return new LlamaForwardRequestParseResult(false, null, errorMessage);
    }
}
