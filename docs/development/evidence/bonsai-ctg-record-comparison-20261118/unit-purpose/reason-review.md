# Unit and reporting-purpose diagnostic

Bonsai 1.7B generated four Logos responses using the v26 evaluator’s captured prompts. The supplied action reading remained fixed within each authority pair. The physical calculation, original proposal, observed text, subject and time were explicit in every prompt. Each pair differed only in the authority sentence. No generated response qualified as a canonical finding under the registered rules.

| Proposed value against 12 mm | Reporting rule | What the model said in its reason | What the reason supports |
| --- | --- | --- | --- |
| 1.2 cm | Copy magnitude and unit as written | It explicitly says 1.2 cm does not equal 12 mm. | Copying requires retaining the spelling, but the proposed length is physically equal. The reason gives an incorrect justification for declining. |
| 1.2 cm | Any SI unit representing the same length | It says 1.2 cm is inconsistent with 12 mm. Its extra text says the evidence cannot support the conversion. | The record and authority support this conversion. This is an over-rejection in the free-text rationale. |
| 1.3 cm | Copy magnitude and unit as written | It says 1.3 cm and 12 mm are physically different and cites the measurement record. | The physical contradiction is correct and sufficient for declining. |
| 1.3 cm | Any SI unit representing the same length | It again identifies physically different measurements and cites the record. | The physical contradiction is correct even when conversion is permissible. |

The two physically equivalent cells produced malformed JSON with an unescaped record list in the source field. The physically unequal cells cited source 2, the evidence, but supplied additional keys and a sentence as the finding. A change limited to parsing or formatting cannot establish the missing equivalence distinction in these readings. No response was converted from free text into a vote.

This controlled English example isolates the effect of changing one purpose sentence at each of two quantities. It does not estimate failure prevalence or establish which prompt element causes the confusion.

The complete per-cell text, finish reason and hashes are in `score.json`. The raw run SHA-256 is `8243a8021c2cb0fd0efd2e8c630b5a73bca4091133b743a2ecfde62018fe2c9c`.
