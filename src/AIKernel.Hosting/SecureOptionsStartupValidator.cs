namespace AIKernel.Hosting;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Security;
using AIKernel.Core.Time;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// ISecureOptions を実装する Options 型に対して、
/// 起動時に秘密情報を解決し、推論リクエストが発生する前に検証するための基底 Startup Validator です。
///
/// このクラスは Provider 固有の追加検証を許可するため、意図的に public かつ non-sealed にしています。
///
/// 派生クラスを作成する場合は、通常 <see cref="ValidateCoreAsync"/> を override してください。
/// StartAsync 自体を override する必要は基本的にありません。
///
/// 設計意図:
/// - Hosting 層は Provider 固有の Options 型を知らない。
/// - Provider 側は、このクラスを継承することで独自検証を追加できる。
/// - 検証失敗時は SecureCredentialException 系列、またはそれを起点とする例外を投げ、
///   Host 起動を停止させることで Fail-Closed を維持する。
///
/// ロギング方針:
/// - LoggerMessage.Define によってログテンプレートを static delegate として事前構築する。
/// - StartAsync 内では IsEnabled でログレベルを確認してから delegate を呼び出す。
/// - typeof(TOptions).FullName のような引数評価も、ログレベルが有効な場合だけ実行する。
///
/// これにより、通常時に不要な文字列生成・テンプレート解析・ボックス化・引数配列生成を避ける。
/// 高頻度起動や多数 Provider の検証が走る環境でも、ログ無効時のオーバーヘッドを最小化できる。
/// </summary>
/// <typeparam name="TOptions">
/// 秘密情報の解決対象となる Options 型。
/// 必ず ISecureOptions を実装する必要があります。
/// </typeparam>
/// <remarks>
/// 派生クラスから追加サービスを受け取りやすくするため、
/// 基底クラスの依存関係は最小限にしています。
///
/// Provider 固有 Validator では、このコンストラクタを呼び出したうえで、
/// ILogger、HttpClient、IOptionsMonitor、独自 Client などを追加注入してください。
/// </remarks>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Hosting.SecureOptionsStartupValidator']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Hosting.SecureOptionsStartupValidator']" />
public class SecureOptionsStartupValidator<TOptions>(
    SecureCredentialResolver<TOptions> resolver,
    ILogger<SecureOptionsStartupValidator<TOptions>>? logger = null,
    IKernelClock? clock = null) : IHostedService
    where TOptions : class, ISecureOptions
{
    private static readonly Action<ILogger, string?, Exception?> LogStarting =
        LoggerMessage.Define<string?>(
            LogLevel.Debug,
            new EventId(1001, nameof(LogStarting)),
            "Starting secure options validation. OptionsType={OptionsType}");

    private static readonly Action<ILogger, string?, Exception?> LogCompleted =
        LoggerMessage.Define<string?>(
            LogLevel.Information,
            new EventId(1002, nameof(LogCompleted)),
            "Secure options validation completed. OptionsType={OptionsType}");

    private static readonly Action<ILogger, string?, Exception?> LogFailed =
        LoggerMessage.Define<string?>(
            LogLevel.Error,
            new EventId(1003, nameof(LogFailed)),
            "Secure options validation failed. OptionsType={OptionsType}");

    private readonly SecureCredentialResolver<TOptions> _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    private readonly IKernelClock _clock = clock ?? KernelClock.System();

    /// <summary>
    /// 派生クラスから必要に応じて資格情報解決器を参照するための protected アクセサです。
    /// ただし、通常の Provider 固有検証では <see cref="ValidateCoreAsync"/> の options を利用すれば十分です。
    /// </summary>
    protected SecureCredentialResolver<TOptions> Resolver => _resolver;

    /// <summary>
    /// 派生クラスで追加ログを出す場合に利用する protected アクセサです。
    ///
    /// 派生クラス側でも、高頻度・高性能を重視する場合は LoggerMessage.Define と IsEnabled ガードを併用してください。
    /// </summary>
    protected ILogger<SecureOptionsStartupValidator<TOptions>>? Logger => logger;

    /// <summary>
    /// 有効期限検証や時刻依存の Provider 固有検証で利用する protected アクセサです。
    /// DateTimeOffset.UtcNow を直接使わず、IKernelClock 経由にすることでテスト容易性を保ちます。
    /// </summary>
    protected IKernelClock Clock => _clock;

    /// <summary>
    /// 起動時検証の入口です。
    ///
    /// このメソッドは原則として override せず、
    /// Provider 固有の検証は <see cref="ValidateCoreAsync"/> を override して追加してください。
    ///
    /// ログ出力では LoggerMessage.Define で作成した static delegate を使用します。
    /// ただし delegate 呼び出し前に必ず IsEnabled を確認します。
    ///
    /// LoggerMessage.Define 自体にもログレベル判定はありますが、
    /// ここで明示的に IsEnabled ガードを置く理由は、delegate に渡す引数の評価も遅延させるためです。
    ///
    /// 例:
    /// typeof(TOptions).FullName は軽量ではありますが、ログ無効時には評価する必要がありません。
    /// より複雑な派生クラスでは、ログ引数の生成に string.Join や LINQ が入る可能性もあります。
    /// IsEnabled ガードを先に置くことで、それらの不要な評価・割り当てを確実に避けます。
    ///
    /// StartAsync の責務:
    /// 1. SecureCredentialResolver により秘密情報を解決する。
    /// 2. 解決済み Options に対して ValidateCoreAsync を呼び出す。
    /// 3. 例外が発生した場合は握りつぶさず、Host 起動を Fail-Closed させる。
    /// </summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureOptionsStartupValidator.StartAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureOptionsStartupValidator.StartAsync']" />
    public virtual async Task StartAsync(CancellationToken cancellationToken)
    {
        if (logger?.IsEnabled(LogLevel.Debug) == true)
        {
            LogStarting(logger, typeof(TOptions).FullName, null);
        }

        try
        {
            var options = await _resolver
                .ResolveAsync(cancellationToken)
                .ConfigureAwait(false);

            await ValidateCoreAsync(options, cancellationToken)
                .ConfigureAwait(false);

            if (logger?.IsEnabled(LogLevel.Information) == true)
            {
                LogCompleted(logger, typeof(TOptions).FullName, null);
            }
        }
        catch (Exception ex)
        {
            if (logger?.IsEnabled(LogLevel.Error) == true)
            {
                LogFailed(logger, typeof(TOptions).FullName, ex);
            }

            throw;
        }
    }

    /// <summary>
    /// Stop 時に解放すべきリソースは基底クラスでは保持していないため、何もしません。
    ///
    /// 派生クラスが外部接続や独自 Client を所有する場合は、
    /// 必要に応じて override してください。
    /// </summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureOptionsStartupValidator.StopAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureOptionsStartupValidator.StopAsync']" />
    public virtual Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 起動時検証の中核です。
    ///
    /// 派生クラスが Provider 固有の検証を追加する場合は、このメソッドを override してください。
    /// override する場合も、原則として最初に base.ValidateCoreAsync(...) を呼び出してください。
    ///
    /// 基底クラスが行う検証:
    /// - ApiKey が解決済みであること。
    /// - ApiKey が空文字でないこと。
    /// - ApiKey に前後空白がないこと。
    /// - ApiKey に制御文字が含まれないこと。
    /// - ApiKey が最低限の長さを満たすこと。
    ///
    /// ここで失敗した場合は SecureCredentialException 系列を投げます。
    /// これにより、推論リクエスト発生前に Host 起動を止める Fail-Closed を維持します。
    /// </summary>
    protected virtual Task ValidateCoreAsync(
        TOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        cancellationToken.ThrowIfCancellationRequested();

        var validationKey = !string.IsNullOrWhiteSpace(options.SecretKeyName)
            ? options.SecretKeyName!
            : $"{typeof(TOptions).Name}.ApiKey";

        SecureCredentialGuard.ValidateSecret(
            validationKey,
            options.ApiKey,
            expiresAtUtc: null,
            timeProvider: _clock.Logical);

        return Task.CompletedTask;
    }
}
