from datetime import date
from decimal import Decimal
import json
from pathlib import Path

import pytest
from pydantic import ValidationError

from adpulse_scoring.engine import score
from adpulse_scoring.models import MetricDirection, ScoringRequest, ScoringResponse


SCORING_FIXTURES = json.loads(
    (Path(__file__).parents[2] / "contracts" / "scoring-v1-fixtures.json").read_text(encoding="utf-8")
)


def request(**overrides) -> ScoringRequest:
    values = {
        "tenant_id": "tenant-a",
        "campaign_id": "campaign-1",
        "metric_name": "cpc",
        "observed": Decimal("2.5"),
        "baseline": Decimal("1"),
        "data_points": 30,
        "minimum_data_points": 7,
        "direction": MetricDirection.LOWER_IS_BETTER,
        "evidence_window_start": date(2026, 1, 1),
        "evidence_window_end": date(2026, 1, 7),
    }
    values.update(overrides)
    return ScoringRequest(**values)


def test_scoring_is_deterministic_and_explainable():
    result = score(request())

    assert result.score == Decimal("100.0000")
    assert result.severity == "critical"
    assert result.confidence == Decimal("1.0000")
    assert result.data_sufficiency == "sufficient"
    assert result.reason_codes == ["degraded_vs_baseline"]
    assert result.fingerprint == "d3b5d107128b24577b84db5e19fd45794396c2d94de804997a44bbe06244d97e"


def test_cold_start_and_zero_baseline_do_not_create_alerts():
    cold_start = score(request(data_points=3))
    zero_baseline = score(request(baseline=Decimal("0")))

    assert cold_start.reason_codes == ["insufficient_data"]
    assert cold_start.data_sufficiency == "insufficient"
    assert cold_start.confidence == Decimal("0.2143")
    assert zero_baseline.reason_codes == ["baseline_unavailable"]
    assert zero_baseline.confidence == Decimal("0")


def test_invalid_window_and_separator_are_rejected():
    with pytest.raises(ValidationError):
        request(evidence_window_end=date(2025, 12, 31))

    with pytest.raises(ValidationError):
        request(tenant_id="tenant|a")


def test_decimal_and_integer_inputs_match_the_contract():
    for field, value in (("observed", 1), ("observed", "01"), ("observed", "1e2"), ("baseline", "NaN")):
        with pytest.raises(ValidationError):
            request(**{field: value})

    with pytest.raises(ValidationError):
        request(data_points=True)

    with pytest.raises(ValidationError):
        request(minimum_data_points=1_000_001)


def test_response_bounds_and_decimal_scale_are_enforced():
    result = score(request(baseline=Decimal("0")))

    assert result.model_dump_json() == (
        '{"score":"0.0000","severity":"none","confidence":"0.0000",'
        '"data_sufficiency":"insufficient","reason_codes":["baseline_unavailable"],'
        '"fingerprint":"8a9cc0d0a90f11eb994d2a5d5d8210549a2d8d07ceac137e5cae2619fe4306ac"}'
    )

    with pytest.raises(ValidationError):
        ScoringResponse(
            score=Decimal("1.23456"),
            severity="critical",
            confidence=Decimal("1"),
            data_sufficiency="sufficient",
            reason_codes=["degraded_vs_baseline"],
            fingerprint="d3b5d107128b24577b84db5e19fd45794396c2d94de804997a44bbe06244d97e",
        )


@pytest.mark.parametrize("fixture", SCORING_FIXTURES, ids=lambda fixture: fixture["name"])
def test_shared_scoring_fixtures(fixture):
    result = score(ScoringRequest.model_validate(fixture["request"]))

    assert result.model_dump(mode="json") == fixture["expected"]
