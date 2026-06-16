# 拒絶ポリシー・ガバナンス
Version: 0.1.2
ID: Canon.CTG.Monolith.Policy.Reject

拒絶ポリシーは、ガバナンスシステム全体で使用される拒絶理由の正典的な分類体系（タクソノミー）を定義する。
拒絶が「いつ」発生するかを決定するものではない；その責務は評議会およびゲートに属する。
本文書は、すべてのレイヤーに対する統一された分類（RejectReasonKind）を提供する。

---

## 1. 目的 (Purpose)

拒絶ポリシーの目的は、拒絶理由コードに対する「信頼できる唯一の情報源（SSoT）」を提供することである。
これらのコードは以下によって使用される：

- 評議会（Logos, Ethos, Pathos）が **Reject** 票を投じる際
- 意思決定ゲートが **Deny** を計算する際
- 軌道ゲートが **Halt** を計算する際

これにより、システム全体で一貫した監査可能性と決定論的リプレイが保証される。

---

## 2. 拒絶理由の分類 (RejectReasonKind Taxonomy)

拒絶理由は、それを発行するレイヤーによってグループ化される。

---

### 2.1 評議会レベルの理由 (Council-Level Reasons)
評議会が Reject 票を投じた場合に**のみ**発行される。
これらの理由は、提案に対する「意味論的（semantic）」な評価を反映する。

- **SAFETY_VIOLATION（安全性違反）**
  行動が危害のリスクをもたらすか、安全原則に違反している。
  （Ethosによって発行される）

- **LOGICAL_INCONSISTENCY（論理的不整合）**
  推論が矛盾している、不完全である、または検証不可能である。
  （Logosによって発行される）

- **CONTEXT_MISALIGNMENT（文脈の不整合）**
  行動がユーザーの意図、感情的状態、または文脈上の境界に違反している。
  （Pathosによって発行される）

- **IRREVERSIBLE_ACTION（不可逆な行動）**
  行動が取り消し不可能であり、正当化を欠いている。
  （Ethosによって発行される）

- **INSUFFICIENT_INFORMATION（情報不足）**
  提案に必要な内部情報が欠落している。
  （LogosまたはPathosによって発行される）

- **OPAQUE_REASONING（不透明な推論）**
  推論が説明可能または監査可能ではない。
  （LogosまたはEthosによって発行される）

これらの理由は **CouncilDecisionTrace** の内部に表示される。

---

### 2.2 ゲートレベルの理由 (Gate-Level Reasons)
ゲート（意思決定ゲート / 軌道ゲート）によって**のみ**発行される。
これらの理由は、ガバナンスプロセスの「構造的（structural）」な結果を反映する。

- **ETHOS_VETO（Ethosの拒否権）**
  Ethosが Reject を投じ、絶対的な拒否権がトリガーされた。
  （意思決定ゲートのみ。注：評議会レベルの診断における「Vetoed」状態は純粋に情報提供であり、正式な構造的拒絶はこのゲートレベルの理由のみである。）

- **FAIL_CLOSED（フェイルクローズ）**
  Approve 票が過半数に達しなかった（Approve < 2）。
  （意思決定ゲートのみ）

- **STEP_DENIED（ステップ拒絶）**
  軌道内のステップが拒絶された。
  （軌道ゲートのみ）

- **IMPLICIT_DENY（暗黙の拒絶）**
  特定の理由が適用されない場合のデフォルトのフォールバック。
  （両方のゲートで使用される）

ゲートレベルの理由は **GovernanceTrace** および **TrajectoryGateTrace** の内部に表示される。

---

## 3. 正典の参照 (Canon Reference)

すべての拒絶には、拒絶を正当化する正典またはガバナンス文書の特定のセクションを指し示す **CanonReference** が含まれなければならない。

例：

- `Canon.CTG.Monolith.Gate.Decision.3.1` — Ethosの拒否権
- `Canon.CTG.Monolith.Council.Logos.3` — 論理的整合性
- `Canon.CTG.Monolith.Council.Pathos.4` — 文脈との整合性
- `Canon.CTG.Monolith.Gate.Decision.3.2` — 多数決規則
- `Canon.CTG.Monolith.Gate.Trajectory.3` — 短絡評価

参照は正確かつ安定的でなければならない。

---

## 4. レイヤーの責務（関心の分離 / SoC）

本文書は拒絶条件を定義**しない**。
それらは以下において定義される：

- `council.logos.monolith.md`
- `council.ethos.monolith.md`
- `council.pathos.monolith.md`
- `gate.decision.monolith.md`
- `gate.trajectory.monolith.md`

拒絶ポリシーは論理ではなく、**分類のみ**を定義する。

---

## 5. 決定論とリプレイ (Determinism and Replay)

RejectReasonKind は以下の性質を持たなければならない：

- 決定論的であること
- レイヤーに特化していること
- ガバナンス証跡のリプレイを通じて再現可能であること
- 明示的に改訂されない限り、バージョン間で安定的であること

同一の入力は、常に同一の理由コードを生成しなければならない。

---

## 6. 改訂 (Amendments)

この分類体系は、Monolith-ROMの将来のバージョンにおいて拡張される可能性がある。
拡張は以下を保持しなければならない：

- 信頼できる唯一の情報源 (SSoT)
- 関心の厳格な分離 (SoC)
- 決定論的分類
- 可能な限りの後方互換性
