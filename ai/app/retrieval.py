"""Retrieval over chunks of official pages (the RAG corpus)."""
from dataclasses import dataclass

from .config import get_settings
from .db import get_pool
from .embeddings import embed_query


@dataclass(frozen=True)
class Chunk:
    source_url: str
    title: str
    content: str
    similarity: float
    fetched_at: str


def search(query: str, url_contains: str | None = None, k: int | None = None) -> list[Chunk]:
    k = k or get_settings().retrieval_top_k
    vector = embed_query(query)
    sql = """
        SELECT source_url, title, content, 1 - (embedding <=> %(v)s) AS similarity, fetched_at
        FROM documents
        WHERE embedding IS NOT NULL
          AND (%(f)s::text IS NULL OR source_url ILIKE '%%' || %(f)s || '%%')
        ORDER BY embedding <=> %(v)s
        LIMIT %(k)s
    """
    with get_pool().connection() as conn:
        rows = conn.execute(sql, {"v": vector, "f": url_contains, "k": k}).fetchall()
    return [Chunk(r[0], r[1], r[2], round(float(r[3]), 4), r[4].date().isoformat()) for r in rows]
