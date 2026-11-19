"""Compass AI service.

Owns everything probabilistic: course-fit scoring (local embeddings + pgvector),
retrieval over official sources, and the counsellor agent.
It never decides eligibility or cost; the Compass API does that with deterministic rules.
"""
import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.responses import StreamingResponse
from pydantic import BaseModel, Field

from . import retrieval
from .agent import ChatRequest, stream_chat
from .embeddings import warm_up
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


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok"}


@app.post("/fit", response_model=FitResponse)
def fit(req: FitRequest) -> FitResponse:
    return score_courses(req)


class SearchRequest(BaseModel):
    query: str = Field(min_length=2, max_length=500)
    site: str | None = None


@app.post("/search")
def search(req: SearchRequest) -> list[retrieval.Chunk]:
    """Retrieval only. Used by the evaluation suite and for debugging."""
    return retrieval.search(req.query, url_contains=req.site)


@app.post("/chat")
async def chat(req: ChatRequest) -> StreamingResponse:
    return StreamingResponse(
        stream_chat(req),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )
