from decimal import Decimal, ROUND_HALF_UP
from hashlib import sha256

from .models import MetricDirection, ScoringRequest, ScoringResponse

_ZERO = Decimal("0.0000")
_ONE = Decimal("1")
_HUNDRED = Decimal("100")
_FOUR_PLACES = Decimal("0.0001")


def score(request: ScoringRequest) -> ScoringResponse:
    confidence = _round(min(_ONE, Decimal(request.data_points) / (Decimal(request.minimum_data_points) * 2)))

    if request.data_points < request.minimum_data_points:
        return _result(request, _ZERO, "none", confidence, "insufficient", "insufficient_data")

    if request.baseline == _ZERO:
        return _result(request, _ZERO, "none", _ZERO, "insufficient", "baseline_unavailable")

    degraded = (
        request.observed > request.baseline
        if request.direction is MetricDirection.LOWER_IS_BETTER
        else request.observed < request.baseline
    )
    if not degraded:
        return _result(request, _ZERO, "none", confidence, "sufficient", "within_baseline")

    deviation = abs(request.observed - request.baseline) / request.baseline
    score_value = _HUNDRED if deviation >= _ONE else _round(deviation * _HUNDRED)
    return _result(request, score_value, _severity(score_value), confidence, "sufficient", "degraded_vs_baseline")


def _result(
    request: ScoringRequest,
    score_value: Decimal,
    severity: str,
    confidence: Decimal,
    data_sufficiency: str,
    reason_code: str,
) -> ScoringResponse:
    fingerprint_input = "|".join(
        (
            request.tenant_id,
            request.campaign_id,
            request.metric_name,
            request.evidence_window_start.isoformat(),
            request.evidence_window_end.isoformat(),
            request.scoring_version,
            reason_code,
        )
    )
    fingerprint = sha256(fingerprint_input.encode("utf-8")).hexdigest()
    return ScoringResponse(
        score=_round(score_value),
        severity=severity,
        confidence=_round(confidence),
        data_sufficiency=data_sufficiency,
        reason_codes=[reason_code],
        fingerprint=fingerprint,
    )


def _severity(score_value: Decimal) -> str:
    if score_value >= Decimal("75"):
        return "critical"
    if score_value >= Decimal("50"):
        return "high"
    if score_value >= Decimal("25"):
        return "medium"
    if score_value >= Decimal("10"):
        return "low"
    return "none"


def _round(value: Decimal) -> Decimal:
    return value.quantize(_FOUR_PLACES, rounding=ROUND_HALF_UP)
