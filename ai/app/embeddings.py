"""Local embeddings: no API calls, no rate limits, ~20-50 ms per query on CPU."""
from functools import lru_cache

import numpy as np
from fastembed import TextEmbedding

from .config import get_settings


@lru_cache
def _model() -> TextEmbedding:
    return TextEmbedding(model_name=get_settings().embedding_model)


def embed_query(text: str) -> np.ndarray:
    return next(iter(_model().query_embed([text])))


def embed_passages(texts: list[str]) -> list[np.ndarray]:
    return list(_model().passage_embed(texts))


def warm_up() -> None:
    embed_query("warm up")
