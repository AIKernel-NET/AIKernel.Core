namespace AIKernel.Core.Providers.SkillProvider;

using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Dsl;

/// <summary>
/// [EN] Loads Skill.MD / SKILL.md files, parses them, compiles their DSL projection, and registers capabilities.
/// [JA] Skill.MD / SKILL.md を読み込み、解析、DSL 投影の compile、capability 登録を行います。
/// </summary>
public sealed class SkillLoader
{
    private readonly SkillManifestParser _parser;

    /// <summary>
    /// [EN] Creates a Skill loader with the default parser.
    /// [JA] 既定 parser を使用する Skill loader を作成します。
    /// </summary>
    public SkillLoader()
        : this(new SkillManifestParser())
    {
    }

    /// <summary>
    /// [EN] Creates a Skill loader with an explicit parser.
    /// [JA] 明示された parser を使用する Skill loader を作成します。
    /// </summary>
    public SkillLoader(
        SkillManifestParser parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// [EN] Loads every Skill.MD / SKILL.md file under a VFS-compatible root path.
    /// [JA] VFS 互換 root path 配下の Skill.MD / SKILL.md をすべて読み込みます。
    /// </summary>
    public IReadOnlyList<SkillManifest> Load(
        string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
        {
            return [];
        }

        return EnumerateSkillFiles(rootPath)
            .Select(LoadFile)
            .OrderBy(x => x.SourcePath, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// [EN] Loads one Skill.MD / SKILL.md file.
    /// [JA] 単一の Skill.MD / SKILL.md file を読み込みます。
    /// </summary>
    public SkillManifest LoadFile(
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var markdown = File.ReadAllText(path);
        return _parser.Parse(markdown, NormalizePath(path));
    }

    /// <summary>
    /// [EN] Loads, compiles, and registers all Skill.MD capability descriptors.
    /// [JA] Skill.MD capability descriptor を読み込み、compile し、登録します。
    /// </summary>
    public async ValueTask<IReadOnlyList<SkillCapabilityDescriptor>> LoadAndRegisterAsync(
        string rootPath,
        ICapabilityModuleRegistry registry,
        IDslPipelineCompiler? compiler = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registry);
        cancellationToken.ThrowIfCancellationRequested();

        var descriptors = new List<SkillCapabilityDescriptor>();
        foreach (var manifest in Load(rootPath))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var descriptor = manifest.ToDescriptor();
            var metadata = new Dictionary<string, string>(
                descriptor.Metadata,
                StringComparer.Ordinal)
            {
                ["skill.dsl_compiled"] = "false"
            };

            if (compiler is not null)
            {
                await compiler.CompileAsync(manifest.Dsl, cancellationToken)
                    .ConfigureAwait(false);
                metadata["skill.dsl_compiled"] = "true";
            }

            var compiledDescriptor = descriptor with
            {
                Metadata = metadata
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            };

            await registry.RegisterAsync(
                    SkillCapabilityContracts.ToContract(compiledDescriptor),
                    cancellationToken)
                .ConfigureAwait(false);

            descriptors.Add(compiledDescriptor);
        }

        return descriptors
            .OrderBy(x => x.CapabilityId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> EnumerateSkillFiles(
        string rootPath)
    {
        return Directory
            .EnumerateFiles(rootPath, "*.md", SearchOption.AllDirectories)
            .Where(path =>
            {
                var name = Path.GetFileName(path);
                return string.Equals(name, "SKILL.md", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(name, "Skill.MD", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(NormalizePath, StringComparer.Ordinal);
    }

    private static string NormalizePath(
        string path)
    {
        return path.Replace('\\', '/');
    }
}
