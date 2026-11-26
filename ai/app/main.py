"""Compass AI service.

Owns everything probabilistic: course-fit scoring (embeddings + pgvector),
retrieval over official sources, and the counsellor agent.
It never decides eligibility or cost; the Compass API does that with deterministic rules.
"""
import logging
from contextlib import asynccontextmanager

import hmac

from fastapi import Depends, FastAPI, Header, HTTPException, Request
from fastapi.responses import JSONResponse, StreamingResponse
from pydantic import BaseModel, Field

from . import retrieval
from .agent import ChatRequest, stream_chat
from .config import get_settings
from .embeddings import EmbeddingQuotaExhausted, warm_up
from .fit import FitRequest, FitResponse, score_courses

logging.basicConfig(level=logging.INFO)
log = logging.getLogger("compass.ai")


@asynccontextmanager
async def lifespan(_: FastAPI):
    try:
        warm_up()  # load the embedding model before the first request, not during it
    except Exception:
        log.exception("Embedding model warm-up failed")
    yield


app = FastAPI(title="Compass AI", version="0.2.0", lifespan=lifespan)


@app.exception_handler(EmbeddingQuotaExhausted)
async def quota_exhausted(_: Request, ex: EmbeddingQuotaExhausted) -> JSONResponse:
    """Daily embedding quota used up: a clear, retryable 503 instead of a 500 with a traceback."""
    log.warning("Embedding quota exhausted: %s", ex)
    return JSONResponse(status_code=503, headers={"Retry-After": "3600"},
                        content={"detail": "Search is temporarily unavailable. Please try again later."})


def require_internal_key(x_internal_key: str | None = Header(default=None)) -> None:
    """The AI service is public on some hosts; only the Compass API (which rate-limits users) may call it."""
    expected = get_settings().internal_api_key
    if expected and not hmac.compare_digest(x_internal_key or "", expected):
        raise HTTPException(status_code=401, detail="Unauthorized")


protected = [Depends(require_internal_key)]


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/fit", response_model=FitResponse, dependencies=protected)
def fit(req: FitRequest) -> FitResponse:
    return score_courses(req)


class SearchRequest(BaseModel):
    query: str = Field(min_length=2, max_length=500)
    site: str | None = None


@app.post("/search", dependencies=protected)
def search(req: SearchRequest) -> list[retrieval.Chunk]:
    """Retrieval only. Used by the evaluation suite and for debugging."""
    return retrieval.search(req.query, url_contains=req.site)


@app.post("/chat", dependencies=protected)
async def chat(req: ChatRequest) -> StreamingResponse:
    return StreamingResponse(
        stream_chat(req),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )
