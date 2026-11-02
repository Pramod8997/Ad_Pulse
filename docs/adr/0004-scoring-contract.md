# ADR-0004: Deterministic scoring contract

Status: Accepted — Phase 1

## Request contract

Contract version `scoring-v1` is the request schema in `contracts/scoring-v1.schema.json`. It contains tenant, campaign and metric identifiers; non-negative observed and baseline decimals; data-point counts; an explicit metric direction; and an inclusive evidence window. Identifiers reject the fingerprint separator (`|`), `data_points` is `0..1,000,000`, `minimum_data_points` is `1..1,000,000`, and the evidence-window end may not precede its start. `scoring_version` defaults to `scoring-v1` and is limited to 32 characters.

Decimal values are canonical JSON strings in the shared contract. The Python Pydantic model currently accepts JSON numbers as a permissive parser superset; the .NET domain model receives native `decimal` values. No input scale or magnitude limit beyond the parser/database types is asserted by this contract.

## Deterministic result

The scorer first computes:

`confidence = round_half_up_4(min(1, data_points / (minimum_data_points * 2)))`

The following ordered rules apply:

1. If `data_points < minimum_data_points`, return score `0`, severity `none`, confidence from the formula, `data_sufficiency = insufficient` and reason `insufficient_data`.
2. If the baseline is zero, return score `0`, severity `none`, confidence `0`, `data_sufficiency = insufficient` and reason `baseline_unavailable`.
3. A lower-is-better metric is degraded when observed is greater than baseline. A higher-is-better metric is degraded when observed is less than baseline. Otherwise return score `0`, severity `none`, the computed confidence, `data_sufficiency = sufficient` and reason `within_baseline`.
4. For a degraded metric, calculate `abs(observed - baseline) / baseline * 100`, round half-up to four decimal places and cap at `100`. Use severity thresholds `low >= 10`, `medium >= 25`, `high >= 50` and `critical >= 75`; values below `10` have severity `none`. The result uses `data_sufficiency = sufficient` and reason `degraded_vs_baseline`.

Confidence describes data sufficiency; it is not a probability. The current engine emits one reason code per result, and cold starts/zero baselines only return a result describing why no alert-worthy score was produced. Neither scorer endpoint creates or persists an alert.

## Fingerprint and ownership

The fingerprint input is the UTF-8 string formed by joining, with literal `|`, the tenant ID, campaign ID, metric name, evidence-window start (`yyyy-MM-dd`), evidence-window end (`yyyy-MM-dd`), scoring version and reason code, in that order. Identifiers reject `|` so this representation is unambiguous. The fingerprint is lowercase hexadecimal SHA-256.

The .NET and Python implementations use the same rules and shared golden fixtures. The Python scorer is stateless; SQL Server and the authorized ASP.NET workflow own membership, persistence and alert lifecycle. Scoring determinism does not by itself authorize a tenant or make a persistence retry a successful no-op.
