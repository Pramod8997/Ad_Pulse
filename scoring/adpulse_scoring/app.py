from fastapi import FastAPI
from fastapi.responses import JSONResponse
from starlette.types import ASGIApp, Message, Receive, Scope, Send

from .engine import score
from .models import ScoringRequest, ScoringResponse

MAX_REQUEST_BYTES = 64 * 1024


class RequestSizeLimitMiddleware:
    def __init__(self, app: ASGIApp, max_body_bytes: int) -> None:
        self.app = app
        self.max_body_bytes = max_body_bytes

    async def __call__(self, scope: Scope, receive: Receive, send: Send) -> None:
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return

        content_length = _content_length(scope)
        if content_length is None and _has_content_length(scope):
            await JSONResponse(status_code=400, content={"detail": "invalid content-length"})(scope, receive, send)
            return
        if content_length is not None and content_length > self.max_body_bytes:
            await JSONResponse(status_code=413, content={"detail": "request body is too large"})(scope, receive, send)
            return

        body = bytearray()
        while True:
            message = await receive()
            if message["type"] == "http.disconnect":
                return
            if message["type"] != "http.request":
                continue
            body.extend(message.get("body", b""))
            if len(body) > self.max_body_bytes:
                await JSONResponse(status_code=413, content={"detail": "request body is too large"})(scope, receive, send)
                return
            if not message.get("more_body", False):
                break

        replayed = False

        async def replay_body() -> Message:
            nonlocal replayed
            if replayed:
                return {"type": "http.disconnect"}
            replayed = True
            return {"type": "http.request", "body": bytes(body), "more_body": False}

        await self.app(scope, replay_body, send)


def _content_length(scope: Scope) -> int | None:
    value = next(
        (value for name, value in scope.get("headers", []) if name.lower() == b"content-length"),
        None,
    )
    if value is None:
        return None
    try:
        content_length = int(value)
    except (TypeError, ValueError):
        return None
    return content_length if content_length >= 0 else None


def _has_content_length(scope: Scope) -> bool:
    return any(name.lower() == b"content-length" for name, _ in scope.get("headers", []))


app = FastAPI(title="AdPulse Scoring", version="scoring-v1")
app.add_middleware(RequestSizeLimitMiddleware, max_body_bytes=MAX_REQUEST_BYTES)


@app.get("/health")
async def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/v1/score", response_model=ScoringResponse)
async def score_endpoint(request: ScoringRequest) -> ScoringResponse:
    return score(request)
