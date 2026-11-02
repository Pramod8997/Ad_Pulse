import re
from datetime import date
from decimal import Decimal
from enum import Enum
from typing import Annotated, Literal

from pydantic import BaseModel, BeforeValidator, ConfigDict, Field, WithJsonSchema, model_validator


class MetricDirection(str, Enum):
    HIGHER_IS_BETTER = "higher_is_better"
    LOWER_IS_BETTER = "lower_is_better"


_DECIMAL_PATTERN = r"^(0|[1-9][0-9]*)(\.[0-9]+)?$"
_DECIMAL_RE = re.compile(_DECIMAL_PATTERN)


def _validate_decimal_input(value: object) -> Decimal:
    if isinstance(value, Decimal):
        if not value.is_finite() or value.is_signed() or value < 0:
            raise ValueError("must be a finite, non-negative decimal")
        return value

    if not isinstance(value, str) or _DECIMAL_RE.fullmatch(value) is None:
        raise ValueError("must be a non-negative decimal string")
    return Decimal(value)


DecimalInput = Annotated[
    Decimal,
    BeforeValidator(_validate_decimal_input),
    WithJsonSchema({"type": "string", "pattern": _DECIMAL_PATTERN}),
]


class ScoringRequest(BaseModel):
    model_config = ConfigDict(extra="forbid")

    tenant_id: str = Field(min_length=1, max_length=128)
    campaign_id: str = Field(min_length=1, max_length=128)
    metric_name: str = Field(min_length=1, max_length=64)
    observed: DecimalInput = Field(ge=0)
    baseline: DecimalInput = Field(ge=0)
    data_points: int = Field(strict=True, ge=0, le=1_000_000)
    minimum_data_points: int = Field(strict=True, ge=1, le=1_000_000)
    direction: MetricDirection
    evidence_window_start: date
    evidence_window_end: date
    scoring_version: str = Field(default="scoring-v1", min_length=1, max_length=32)

    @model_validator(mode="after")
    def validate_window_and_fingerprint_parts(self) -> "ScoringRequest":
        if self.evidence_window_end < self.evidence_window_start:
            raise ValueError("evidence_window_end must not precede evidence_window_start")
        if any(
            not value.strip()
            for value in (self.tenant_id, self.campaign_id, self.metric_name, self.scoring_version)
        ):
            raise ValueError("identifier values must not be blank")
        if any("|" in value for value in (self.tenant_id, self.campaign_id, self.metric_name, self.scoring_version)):
            raise ValueError("identifier values cannot contain the fingerprint separator")
        return self


class ScoringResponse(BaseModel):
    model_config = ConfigDict(extra="forbid")

    score: Decimal = Field(ge=0, le=100, decimal_places=4)
    severity: Literal["none", "low", "medium", "high", "critical"]
    confidence: Decimal = Field(ge=0, le=1, decimal_places=4)
    data_sufficiency: Literal["sufficient", "insufficient"]
    reason_codes: list[str] = Field(min_length=1, max_length=4)
    fingerprint: str = Field(min_length=64, max_length=64, pattern=r"^[0-9a-f]{64}$")
