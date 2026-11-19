"""Course-fit scoring: how well a course's content matches a student's interests."""
from pydantic import BaseModel, Field

from .config import get_settings
from .db import get_pool
from .embeddings import embed_query


class FitRequest(BaseModel):
    interests: list[str] = Field(min_length=1, max_length=20)
    course_ids: list[int] = Field(alias="courseIds", min_length=1, max_length=500)

    model_config = {"populate_by_name": True}


class FitResponse(BaseModel):
    scores: dict[int, float]


def to_fit_score(similarity: float, low: float, high: float) -> float:
    """Map cosine similarity onto 0..1. Below `low` is no fit, above `high` is a full fit."""
    if high <= low:
        raise ValueError("high must be greater than low")
    return round(min(1.0, max(0.0, (similarity - low) / (high - low))), 4)


def interests_to_query(interests: list[str]) -> str:
    cleaned = [i.strip() for i in interests if i.strip()]
    return "Master's course covering " + ", ".join(cleaned)


def score_courses(req: FitRequest) -> FitResponse:
    s = get_settings()
    vector = embed_query(interests_to_query(req.interests))
    sql = """
        SELECT id, 1 - (embedding <=> %s) AS similarity
        FROM courses
        WHERE id = ANY(%s) AND embedding IS NOT NULL
    """
    with get_pool().connection() as conn:
        rows = conn.execute(sql, (vector, req.course_ids)).fetchall()
    return FitResponse(scores={
        course_id: to_fit_score(float(sim), s.fit_similarity_low, s.fit_similarity_high)
        for course_id, sim in rows
    })
