# Reject Policy Governance
Version: 0.1.2
ID: Canon.CTG.Monolith.Policy.Reject

The Reject Policy defines the canonical taxonomy of rejection reasons used across the governance system.
It does not determine *when* a rejection occurs; that responsibility belongs to the councils and gates.
This document provides a unified classification (RejectReasonKind) for all layers.

---

## 1. Purpose

The purpose of the Reject Policy is to provide a single source of truth (SSoT) for rejection reason codes.
These codes are used by:

- Councils (Logos, Ethos, Pathos) when casting **Reject**
- Decision Gate when computing **Deny**
- Trajectory Gate when computing **Halt**

This ensures consistent auditability and deterministic replay across the system.

---

## 2. RejectReasonKind Taxonomy

Reject reasons are grouped by the layer that issues them.

---

### 2.1 Council-Level Reasons
Issued **only** by councils when they cast a Reject vote.
These reasons reflect *semantic* evaluation of the proposal.

- **SAFETY_VIOLATION**
  The action introduces risk of harm or violates safety principles.
  (Issued by Ethos)

- **LOGICAL_INCONSISTENCY**
  The reasoning is contradictory, incomplete, or unverifiable.
  (Issued by Logos)

- **CONTEXT_MISALIGNMENT**
  The action violates user intent, emotional state, or contextual boundaries.
  (Issued by Pathos)

- **IRREVERSIBLE_ACTION**
  The action cannot be undone and lacks justification.
  (Issued by Ethos)

- **INSUFFICIENT_INFORMATION**
  The proposal lacks required internal information.
  (Issued by Logos or Pathos)

- **OPAQUE_REASONING**
  The reasoning is not explainable or auditable.
  (Issued by Logos or Ethos)

These reasons appear inside **CouncilDecisionTrace**.

---

### 2.2 Gate-Level Reasons
Issued **only** by gates (Decision Gate / Trajectory Gate).
These reasons reflect *structural* outcomes of the governance process.

- **ETHOS_VETO**
  Ethos cast Reject, triggering absolute veto.
  (Decision Gate only. Note: Any "Vetoed" status in council-level diagnostics is purely informational; the formal structural rejection is exclusively this gate-level reason.)

- **FAIL_CLOSED**
  Approve votes did not reach majority (Approve < 2).
  (Decision Gate only)

- **STEP_DENIED**
  A step in the trajectory was denied.
  (Trajectory Gate only)

- **IMPLICIT_DENY**
  Default fallback when no specific reason applies.
  (Used by both gates)

Gate-level reasons appear inside **GovernanceTrace** and **TrajectoryGateTrace**.

---

## 3. Canon Reference

Every rejection must include a **CanonReference** pointing to the specific section of the Canon or governance document that justifies the rejection.

Examples:

- `Canon.CTG.Monolith.Gate.Decision.3.1` — Ethos veto
- `Canon.CTG.Monolith.Council.Logos.3` — Logical consistency
- `Canon.CTG.Monolith.Council.Pathos.4` — Contextual alignment
- `Canon.CTG.Monolith.Gate.Decision.3.2` — Majority rule
- `Canon.CTG.Monolith.Gate.Trajectory.3` — Short-circuit evaluation

The reference must be precise and stable.

---

## 4. Layer Responsibilities (SoC)

This document does **not** define rejection conditions.
Those are defined in:

- `council.logos.monolith.md`
- `council.ethos.monolith.md`
- `council.pathos.monolith.md`
- `gate.decision.monolith.md`
- `gate.trajectory.monolith.md`

Reject Policy defines **only the classification**, not the logic.

---

## 5. Determinism and Replay

RejectReasonKind must be:

- Deterministic
- Layer-specific
- Reproducible through governance trace replay
- Stable across versions unless explicitly amended

Identical inputs must always produce identical reason codes.

---

## 6. Amendments

This taxonomy may be extended in future versions of the Monolith-ROM.
Extensions must preserve:

- Single Source of Truth (SSoT)
- Strict separation of concerns (SoC)
- Deterministic classification
- Backward compatibility where possible
