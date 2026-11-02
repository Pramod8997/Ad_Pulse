import asyncio

from fastapi.testclient import TestClient

from adpulse_scoring.app import MAX_REQUEST_BYTES, RequestSizeLimitMiddleware, app


def test_health_and_score_contract():
    client = TestClient(app)
    assert client.get("/health").json() == {"status": "ok"}
    response = client.post(
        "/v1/score",
        json={
            "tenant_id": "tenant-a",
            "campaign_id": "campaign-1",
            "metric_name": "cpc",
            "observed": "2.5",
            "baseline": "1",
            "data_points": 30,
            "minimum_data_points": 7,
            "direction": "lower_is_better",
            "evidence_window_start": "2026-01-01",
            "evidence_window_end": "2026-01-07",
        },
    )
    assert response.status_code == 200
    assert response.json() == {
        "score": "100.0000",
        "severity": "critical",
        "confidence": "1.0000",
        "data_sufficiency": "sufficient",
        "reason_codes": ["degraded_vs_baseline"],
        "fingerprint": "d3b5d107128b24577b84db5e19fd45794396c2d94de804997a44bbe06244d97e",
    }


def test_oversized_request_is_rejected_before_parsing():
    client = TestClient(app)
    response = client.post("/v1/score", content="{}", headers={"content-length": str(64 * 1024 + 1)})
    assert response.status_code == 413


def test_invalid_content_length_is_rejected():
    client = TestClient(app)
    response = client.post("/v1/score", content="{}", headers={"content-length": "not-a-number"})
    assert response.status_code == 400


def test_oversized_body_without_content_length_is_rejected():
    messages = [{"type": "http.request", "body": b"x" * (MAX_REQUEST_BYTES + 1), "more_body": False}]
    sent = []

    async def receive():
        return messages.pop(0)

    async def send(message):
        sent.append(message)

    async def downstream(scope, receive, send):
        raise AssertionError("oversized request reached the application")

    scope = {
        "type": "http",
        "asgi": {"version": "3.0"},
        "http_version": "1.1",
        "method": "POST",
        "scheme": "http",
        "path": "/v1/score",
        "raw_path": b"/v1/score",
        "query_string": b"",
        "headers": [],
    }
    asyncio.run(RequestSizeLimitMiddleware(downstream, MAX_REQUEST_BYTES)(scope, receive, send))

    assert sent[0]["status"] == 413
