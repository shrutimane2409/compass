"""Embeddings via Gemini's free embedding API (768 dimensions, matches the schema).

Queries and documents use different task types, which improves retrieval quality.
Query vectors are cached in-process, so repeated questions and fit lookups don't spend quota.
"""
import time
from collections import deque
from functools import lru_cache

import numpy as np
from langchain_google_genai import GoogleGenerativeAIEmbeddings

from .config import get_settings

DIMENSIONS = 768
# Free tier: 100 embed requests (one per text) and 30,000 tokens per minute. Stay under both
# with a rolling-window budget; tokens are estimated at ~3 characters each to leave headroom.
WINDOW_SECONDS = 60
REQUESTS_PER_WINDOW = 90
TOKENS_PER_WINDOW = 24_000
BATCH_SIZE = 40
RETRY_WAIT_SECONDS = 62
MAX_RETRIES = 3

_spent: deque[tuple[float, int, int]] = deque()  # (time, requests, estimated tokens)


def _estimate_tokens(texts: list[str]) -> int:
    return sum(len(t) // 3 + 1 for t in texts)


def _wait_for_budget(requests: int, tokens: int) -> None:
    while True:
        now = time.monotonic()
        while _spent and now - _spent[0][0] >= WINDOW_SECONDS:
            _spent.popleft()
        used_r = sum(r for _, r, _ in _spent)
        used_t = sum(t for _, _, t in _spent)
        if not _spent or (used_r + requests <= REQUESTS_PER_WINDOW and used_t + tokens <= TOKENS_PER_WINDOW):
            _spent.append((now, requests, tokens))
            return
        time.sleep(WINDOW_SECONDS - (now - _spent[0][0]) + 0.5)


class EmbeddingsUnavailable(RuntimeError):
    """No embedding provider is configured."""


class EmbeddingQuotaExhausted(RuntimeError):
    """The provider's daily embedding quota is used up; waiting minutes won't help."""


DAILY_QUOTA_MESSAGE = "Gemini's daily embedding quota is used up (resets at midnight Pacific)."
# After a daily-quota error, skip calls for a while instead of asking Google again on every request.
QUOTA_COOLDOWN_SECONDS = 15 * 60
_quota_exhausted_until = 0.0


def _is_daily_quota(ex: Exception) -> bool:
    return "PerDay" in str(ex)


def _check_cooldown() -> None:
    if time.monotonic() < _quota_exhausted_until:
        raise EmbeddingQuotaExhausted(DAILY_QUOTA_MESSAGE)


def _quota_exhausted() -> EmbeddingQuotaExhausted:
    global _quota_exhausted_until
    _quota_exhausted_until = time.monotonic() + QUOTA_COOLDOWN_SECONDS
    return EmbeddingQuotaExhausted(DAILY_QUOTA_MESSAGE)


@lru_cache
def _client() -> GoogleGenerativeAIEmbeddings:
    s = get_settings()
    if not s.gemini_api_key:
        raise EmbeddingsUnavailable("Set GEMINI_API_KEY.")
    return GoogleGenerativeAIEmbeddings(model=s.embedding_model, google_api_key=s.gemini_api_key)


@lru_cache(maxsize=1024)
def _query_vector(text: str) -> tuple[float, ...]:
    return tuple(_client().embed_query(text, task_type="RETRIEVAL_QUERY", output_dimensionality=DIMENSIONS))


def embed_query(text: str) -> np.ndarray:
    """Raises EmbeddingQuotaExhausted when the daily quota is used up (failures are not cached)."""
    _check_cooldown()
    try:
        return np.asarray(_query_vector(text.strip()), dtype=np.float32)
    except Exception as ex:
        if _is_daily_quota(ex):
            raise _quota_exhausted() from ex
        raise


def embed_passages(texts: list[str]) -> list[np.ndarray]:
    """Embed in small batches, waiting out the free tier's per-minute limit instead of failing.

    The daily limit can't be waited out, so it raises EmbeddingQuotaExhausted immediately.
    """
    _check_cooldown()
    vectors: list[np.ndarray] = []
    for start in range(0, len(texts), BATCH_SIZE):
        batch = texts[start:start + BATCH_SIZE]
        for attempt in range(MAX_RETRIES + 1):
            _wait_for_budget(len(batch), _estimate_tokens(batch))
            try:
                out = _client().embed_documents(batch, task_type="RETRIEVAL_DOCUMENT", output_dimensionality=DIMENSIONS)
                break
            except Exception as ex:  # langchain wraps the 429 in its own error type
                if _is_daily_quota(ex):
                    raise _quota_exhausted() from ex
                if "RESOURCE_EXHAUSTED" not in str(ex) or attempt == MAX_RETRIES:
                    raise
                print(f"  embedding quota reached; waiting {RETRY_WAIT_SECONDS}s", flush=True)
                time.sleep(RETRY_WAIT_SECONDS)
        vectors.extend(np.asarray(v, dtype=np.float32) for v in out)
    return vectors


def warm_up() -> None:
    _client()  # validates configuration without spending quota
