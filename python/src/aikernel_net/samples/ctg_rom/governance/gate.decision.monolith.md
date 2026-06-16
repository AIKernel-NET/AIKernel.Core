# Decision Gate Governance
Version: 0.1.2
ID: Canon.CTG.Monolith.Gate.Decision

The Decision Gate is a deterministic mathematical function that evaluates the votes of the three councils.
It does not interpret content, assess safety, or analyze reasoning.
Its sole responsibility is to compute the final decision from the discrete votes provided.

---

## 1. Purpose

The purpose of the Decision Gate is to provide a pure, deterministic, and auditable mechanism for combining council votes.
It implements the Canonical Triadic Governance (CTG) rule:

```text
if ethosVeto(e) then Deny else if approveCount(l, e, p) >= 2 then Allow else Deny
````

This rule is the complete and final definition of the Decision Gate.

## 2. Inputs

The Decision Gate receives exactly one discrete vote from each council:

- **Logos** — Approve / Abstain / Reject
    
- **Ethos** — Approve / Abstain / Reject
    
- **Pathos** — Approve / Abstain / Reject
    

The input contract (e.g., `GateInput` DTO) must be strictly limited to these three discrete votes. Any continuous carriers (such as `Confidence` or `RiskScore`) attached to council decisions are strictly for telemetry and MUST be completely isolated from or ignored by the Decision Gate. The Decision Gate does not interpret or override council semantics.

## 3. Core Rules

### 3.1 Ethos Veto

If Ethos casts **Reject**, the decision is immediately **Deny**. This rule overrides all other logic.

### 3.2 Majority Approval

If Ethos does not veto, the gate counts the number of **Approve** votes:

- Approve ≥ 2 → **Allow**
    
- Approve ≤ 1 → **Deny** (Fail-Closed)
    

This rule is deterministic and exhaustive.

### 3.3 Fail-Closed

Any vote combination that does not satisfy the majority rule results in **Deny**. This includes Abstain-heavy patterns such as:

- Approve = 1
    
- Approve = 0
    
- Approve = 1 + Abstain = 2
    
- All Abstain
    

The Decision Gate does not distinguish reasons for Abstain; it only counts Approve.

## 4. Evaluation Procedure

The Decision Gate must evaluate proposals in the following exact order:

1. **Check Ethos veto**
    
    - If Ethos = Reject → **Deny**
        
2. **Count Approve votes**
    
    - If Approve ≥ 2 → **Allow**
        
    - Otherwise → **Deny**
        

This is the complete evaluation procedure. No additional checks, heuristics, or conditions are permitted.

## 5. Output

The Decision Gate returns:

- **Allow**
    
- **Deny**
    

Additionally, it must produce:

- **GateDecisionKind**
    
- **RejectReasonKind** (if Deny, sourced from `Canon.CTG.Monolith.Policy.Reject`)
    
- **GovernanceTrace**, containing:
    
    - Council votes
        
    - Applied rule (Ethos veto or majority rule)
        
    - Final decision
        

No continuous values (confidence, risk, probability, score) may be included. All outputs must be discrete and deterministic.

## 6. Scope and Responsibility

The Decision Gate:

- Does **not** evaluate safety
    
- Does **not** evaluate reasoning
    
- Does **not** check for missing information
    
- Does **not** interpret emotional context
    
- Does **not** analyze transparency or reversibility
    
- Does **not** inspect proposal content
    

All such evaluations belong exclusively to the councils. The Decision Gate is a pure function over three discrete votes.

## 7. Determinism and Replay

The Decision Gate must produce identical results for identical inputs. All decisions must be reproducible through governance trace replay. Non-deterministic behavior is prohibited.

## 8. Amendments

This document may be revised in future versions of the Monolith-ROM. Revisions must preserve the principles of determinism, purity, and strict separation of concerns.
