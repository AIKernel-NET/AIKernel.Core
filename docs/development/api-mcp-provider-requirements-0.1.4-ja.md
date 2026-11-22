# AIKernel 0.1.4 機能要求 — 分散アプリケーションの HTTP API・MCP プロバイダー

対象は .NET 10、Windows x64／Linux x64。最初の利用者は TheoriaCast.Music。本文は 0.1.4 向けの実装依頼で、インタフェース名・パッケージ配置・既定時間は提案である。

## 何ができればリリースできるか

GPU サーバーの管理者は外部作曲ツールのパス等を事前設定し、アプリを起動する。AIKernel が API と MCP の待受を開始し、クライアントに渡せる `connection.json` を書き出す。家族の PC では Atelier を初回起動し、そのファイルを取り込む。別サーバーも同じ操作で追加できる。ブラウザと Codex は選択したサーバーの同じ業務処理を利用する。クラウドの HTTPS・リバースプロキシと、イントラネットのホストで同じ契約を使う。

## 既存実装と責任分担

| 所有者 | 既存実装・0.1.4 で担当すること |
|---|---|
| AIKernel の公開契約 | `IKernelModule.ConfigureProviders` と `IProviderRegistrar.Register/TryResolve` を拡張の入口として使う。通信方式や SDK に依存しない操作記述・接続情報・生存状態の DTO と、差し替え可能なインタフェースを提供する。 |
| AIKernel の標準プロバイダー | ASP.NET Core/Kestrel の HTTP ホスト、MCP Streamable HTTP、Bearer 認証、接続ファイルとクライアント側接続先管理、ハートビートを実装する。MCP SDK 型と ASP.NET Core 型は具体実装内に置く。 |
| TheoriaCast.Music.Api／Mcp | 作曲の HTTP ルートと MCP ツールを記述し、同じアプリケーションサービスを DI で呼ぶ。予約、進捗、キャンセル、採否、音源・出自、GPU ワーカーのポーリング・優先制御・読み修正は Music が所有する。 |
| TheoriaCast.Music.Atelier | localhost のブラウザ画面を提供する。ファイル選択、接続先の追加・切り替え、生存状態の表示、Codex の提案を下書きに反映する操作を担当する。 |
| 利用者 | 独立した作曲ツール・モデルを導入し、サーバーでパス・実行条件を設定する。基盤の配布に含めるのはホスト、クライアントと連携コード。 |

Music の従来版は `AIKernel.Hosting` 0.1.3 の `AddAIKernelCore` で DI を構成し、HTTP ルートと MCP の登録はアプリ側で行っていた。開発ツリーには `IKernelHost.StartAsync/StopAsync`、`AIKernelCoreBuilder`、秘密の取得に使う `ISecureCredentialProvider.GetSecretAsync` と `PrivateFileCredentialProvider` がある。既存の資格情報取得を再利用し、受信要求のトークン検証・失効判定を別の責務として追加する。

## 抽出する契約案

| 契約候補 | 必要な入出力・挙動 |
|---|---|
| `IApiEndpointProvider` | アプリが HTTP メソッド、相対パス、操作 ID、認証範囲、要求・結果の種類を登録する。JSON 応答に加え、ストリーム入出力、HTTP Range、状態コードを保持する。ホストが同一メソッド・パスの重複を起動時に検出する。 |
| `IMcpToolProvider` | 名前、説明、入力 JSON Schema、実行先を列挙する。ホストが `initialize`、`tools/list`、`tools/call` を処理し、入力、認証済み呼び出し元、キャンセルをアプリに渡す。ツール名重複は起動時に検出する。 |
| `IRequestTokenValidator` | Bearer を検証して資格情報の識別子と許可範囲を返す。標準実装は永続化したランダムトークンを使う。環境変数・秘密ファイルなどの解決には既存の資格情報プロバイダーを使う。 |
| `IConnectionDescriptorProvider`、`IConnectionProfileStore` | 前者が実際の待受と広告 URL からバージョン付きの接続ファイルを生成・検証する。後者が端末側の追加、一覧、選択、同じサーバーの更新、再起動後の復元を受け持つ。一覧の DTO は秘密を返さない。 |
| `IHeartbeatProvider` | サーバー側は識別子、稼働状態、UTC 応答時刻を返す。クライアント側は登録先別に観測結果を保持し、状態変化を UI に通知できる。GPU の空き状況は業務 API の別項目とする。 |

公開契約は `AIKernel.Abstractions` と `AIKernel.Dtos` の既存の分割に従う。ホスト構築と DI 配線は `AIKernel.Hosting` にまとめ、ASP.NET Core と MCP SDK を必要とする具体実装を選択可能にする。既存プロバイダーや通常の Generic Host 利用者は、0.1.4 に更新しても HTTP 待受を明示的に登録するまで起動しない。

**呼び出し例**　Music が同じ「予約登録」ハンドラーに `POST /jobs` と MCP の `submit_job` を割り当てる。登録処理は1回だけ実行され、両方から同じ予約 ID を照会できる。GUI 支援では `get_studio_request` が指定した相談を読み、`suggest_in_studio` がその画面へ変更案を返す。画面での採用と曲の予約は独立した操作である。汎用ホストは歌詞・GPU・SQLite のスキーマを持たない。

## 接続ファイル・認証・生存確認

**起動とエクスポート**　ホストは待受成功後にファイルを原子的に生成し、そのパスだけを案内する。バインド失敗時は有効な新規ファイルを残さない。サーバー ID は再起動・トークン更新をまたいで維持する。ローカルのバインド先、クライアント向けの公開ベース URL、ファイル出力先を個別に設定できる。自動ポートは実ポートに解決し、外部向け HTTPS、非標準ポート、パス接頭辞を保持する。信頼するプロキシを設定した場合だけ転送ヘッダーを採用する。

```json
{
  "schema_version": 1,
  "service": "TheoriaCast.Music.Api",
  "server_id": "c2e17b1badf045c58b5781c00255d9fe",
  "name": "家庭の作曲サーバー",
  "api_url": "https://music.example.net/family",
  "mcp_url": "https://music.example.net/family/mcp",
  "token": "<このクライアント向けに発行するBearerトークン>"
}
```

**資格情報の境界**　接続ファイルは秘密を含む。静的配信ディレクトリの外に保存し、標準実装では OS の所有者向けアクセス制御を使う。ログ、例外、URL、接続先一覧にはトークンを出さない。クラウド越しは証明書を検証する HTTPS を使う。閉域での平文 HTTP は運用者が明示的に選べる。クライアント用とワーカー用の資格情報を分離し、Atelier 用ファイルにはクライアント用だけを含める。無効・失効資格情報は 401、有効だが許可範囲外なら 403。ローテーション後に旧トークンを拒否し、再インポートで復旧できる。

**複数サーバー**　初回は登録先ゼロで起動し、最初のインポート後に接続する。同じ ID の再インポートで URL や資格情報を更新し、異なる ID は別サーバーとして保存する。端末で選択中の接続先と秘密を対応付ける。切り替え中の要求やリダイレクトで、他の接続先にトークン・下書き・更新操作を渡さない。Atelier はループバックで待ち受け、ブラウザ向け応答は資格情報を含まない。

**ハートビート**　認証付き `GET <api_url>/heartbeat` を用意する。応答には `service`、`server_id`、`status`、`utc` を含め、応答をキャッシュしない。クライアント起動時・インポート時に直ちに確認し、以後は全登録先を個別に監視する。推奨初期値は15秒間隔・4秒タイムアウトで、設定変更可能とする。未確認・接続中・応答なし・認証エラー・サーバー ID 不一致を区別し、最終確認時刻と最終成功時刻を返す。停止・復帰・失効を曲の実行なしで観測できる。

## 0.1.4 のリリース判定

以下はリリース候補で実行する受入試験。各行の合格を再現する最小サンプルと自動試験を添付する。

| 試験 | 合格条件 |
|---|---|
| プロバイダーの差し替え | 作曲依存を持たないテスト用操作を1つ登録。標準 HTTP／MCP とテスト用プロバイダーを同じ契約で差し替えられる。既存アプリの `AddAIKernelCore` だけの起動も通る。 |
| 起動・接続ファイル | Windows／Linux で実待受後にファイルが生成され、再起動で ID を維持する。ポート競合、未知のスキーマ、壊れた JSON は明示的に失敗し、既存の登録内容を失わない。 |
| LAN・クラウド | LAN と、TLS 終端＋パス接頭辞付きリバースプロキシの2台を同じクライアントに登録。再起動後も選択を復元し、A用のトークンでBへ認証しない。 |
| 認証 | REST、MCP 初期化・列挙・呼び出し、ハートビートで無認証または失効を拒否。クライアント資格情報でワーカー専用操作を拒否。秘密は配布アーカイブ・静的配信・ログに含まれない。 |
| 生存確認 | 2台のうち1台を停止すると期限内にその1台だけが応答なしになる。復帰時は自動復帰し、資格情報の失効とサーバー ID 不一致は個別に表示できる。 |
| 実際の Codex と GUI | Codex で MCP ツールを列挙して呼び、HTTP から同じ結果を取得。GUI で発行した相談 ID に対する回答が元の画面に届く。別サーバー・別タブの下書きは保たれる。 |
| データ転送 | 中継経由の Range 再生が 206 と正しい範囲を返す。大きな成果物はストリームで転送し、クライアントの切断／キャンセルが下流まで伝わる。 |

出荷順は **共有契約と DTO → 標準プロバイダーと Hosting 配線 → サンプル・受入試験 → Music の参照切り替え**。Music 側の試作は適合性を確認する材料として使う。AIKernel に公開した契約での実機試験が終わった時点で、Music の暫定的な共通処理を取り除く。

## 照合したコード

- AIKernel.Core の `dev`、参照時点 `b1e8bf7`。[起動時の DI 登録](../../src/AIKernel.Hosting/AIKernelCoreHostingExtensions.cs)、[認証情報の解決](../../src/AIKernel.Hosting/SecureHostingExtensions.cs)、[モデルプロバイダー登録の既存例](../../src/AIKernel.Hosting/ModelProviderHostingExtensions.cs)。
- AIKernel.NET の公開契約、参照時点 `69f66ba`。`src/AIKernel.Abstractions/Hosting/IKernelModule.cs`、`IProviderRegistrar.cs`、`src/AIKernel.Abstractions/Security/ISecureCredentialProvider.cs`。これらは別リポジトリにある。
