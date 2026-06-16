# Trajectory Gate Governance
Version: 0.1.2
ID: Canon.CTG.Monolith.Gate.Trajectory

The Trajectory Gate is a deterministic mathematical function over a sequence of decision results.
It does not interpret content, assess safety, or analyze reasoning.
Its sole responsibility is to evaluate whether an entire trajectory is valid, based on the outputs of the Decision Gate.

---

## 1. Purpose

The purpose of the Trajectory Gate is to provide continuous, yet purely discrete, governance over a sequence of steps.
Safety, reversibility, and auditability are evaluated by the councils at each step. The Trajectory Gate only aggregates Decision Gate results.
While the Decision Gate evaluates a single decision point, the Trajectory Gate evaluates whether **all** steps in a trajectory have been allowed.

Conceptually, it implements the canonical rule:

> A trajectory is allowed if and only if every step’s Decision Gate result is Allow.

---

## 2. Inputs

The Trajectory Gate receives:

- A finite sequence of step-level governance results, each containing:
  - The **Decision Gate** outcome for that step: **Allow / Deny**
  - The associated **StepGovernanceTrace**

The Trajectory Gate does not call councils, does not re-evaluate decisions, and does not inspect proposal content.
It operates solely on the already-computed Decision Gate results.

---

## 3. Core Rule

Let a trajectory $T$ be a sequence of steps $s_1, s_2, \dots, s_n$,
and let $G(s_i)$ be the Decision Gate result for step $s_i$.

The Trajectory Gate implements the following rule:

- If **for all** $i \in \{1, \dots, n\}$, $G(s_i) = \text{Allow}$, then the trajectory is **Continue**.
- If **there exists** any $i$ such that $G(s_i) = \text{Deny}$, then the trajectory is **Halt**.
- If the sequence is empty ($n=0$), it is a precondition violation and the trajectory is **Halt**.

This can be seen as a short-circuiting product over discrete decisions:
- One Deny is sufficient to Halt the entire trajectory.

---

## 4. Evaluation Procedure

The Trajectory Gate must evaluate trajectories in the following exact way:

1. If the sequence is empty, return **Halt**.
2. Iterate over the sequence of step results in order.
3. For each step:
   - If the step’s Decision Gate result is **Deny**, immediately return **Halt**.
4. If the end of the sequence is reached with no Deny:
   - Return **Continue**.

No additional checks, heuristics, or conditions are permitted.
The Trajectory Gate does not generate new decisions; it only aggregates existing ones.

---

## 5. Output

The Trajectory Gate returns:

- **Continue** — All steps are allowed.
- **Halt** — At least one step is denied, or the sequence is empty.

Additionally, it must produce a **TrajectoryGateTrace** containing:

- The list of **StepGovernanceTrace** entries
- The index (or identifier) of the first failing step, if any
- The final trajectory decision (Continue / Halt)

No continuous values (confidence, risk, probability, score) may be included.
All outputs must be discrete and deterministic.

---

## 6. Scope and Responsibility

The Trajectory Gate:

- Does **not** evaluate safety
- Does **not** evaluate reversibility
- Does **not** check for missing information
- Does **not** interpret emotional context
- Does **not** analyze logical structure

All such evaluations belong exclusively to the councils and the Decision Gate at each step.
The Trajectory Gate is a pure function over a sequence of Decision Gate outcomes.

---

## 7. Determinism and Replay

The Trajectory Gate must produce identical results for identical input sequences.
Given the same ordered list of step decisions, the trajectory decision must always be the same.
All trajectory decisions must be reproducible through governance trace replay.
Non-deterministic behavior is prohibited.

---

## 8. Amendments

This document may be revised in future versions of the Monolith-ROM.
Revisions must preserve the principles of determinism, purity, short-circuit evaluation, and strict separation of concerns.
