# 分散アプリケーションの HTTP／MCP ホスト

.NET 10 の Windows x64／Linux x64 で、一つのサービスを HTTP と MCP Streamable HTTP の両方から利用できます。共有契約は AIKernel.NET、待受と接続管理は `AIKernel.Providers.Distributed`、Generic Host との連携は `AIKernel.Hosting` が担当します。アプリケーションは操作の意味、データの保存先、画面を定義します。

## 起動と接続

1. アプリケーションサービスを DI に登録し、公開する操作を `IApiEndpointProvider` と `IMcpToolProvider` で記述します。
2. `AddDistributedApplicationProvider` を登録し、`AddAIKernelCore().WithDistributedApplication()` でホストの生存期間に待受を結び付けます。初回起動では、ループバックの空きポートで待ち受けた後に接続ファイルを生成します。
3. クライアントは `IConnectionProfileStore.Import` に接続ファイルを渡します。`IConnectionClient` には操作の対象となるサーバー ID を指定します。接続ファイルには Bearer トークンが含まれるため、非公開ディレクトリに保管します。
4. `IHeartbeatProvider` から到達性、認証失敗、サーバー ID の不一致を確認できます。既定の確認間隔は15秒、応答期限は4秒です。

## 試験で確認すること

メモのサンプルでは MCP の `save_note` が返した ID を使い、HTTP の `GET /notes/{id}` から本文を読み取ります。受入試験は独立した接続先の選択、資格情報の失効、停止・復帰、リバースプロキシ配下の URL、Range とキャンセルも確認します。業務アプリ固有の互換性は、そのアプリが公開パッケージを参照して検証します。

[メモの起動手順](https://github.com/AIKernel-NET/AIKernel.Providers/tree/dev/samples/DistributedApplication) · [受入試験](https://github.com/AIKernel-NET/AIKernel.Providers/tree/dev/tests/Hosting/AIKernel.Providers.Distributed.Tests) · [Core のホスト登録](../../src/AIKernel.Hosting/DistributedHostingExtensions.cs)
