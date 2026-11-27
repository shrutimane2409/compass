"""Read-only tools for the counsellor agent.

Facts (eligibility, cost) come from the Compass API's deterministic rules engine.
Explanations come from official pages via retrieval. No tool can write data.
"""
import json
from typing import Literal

import httpx
from langchain_core.tools import tool
from pydantic import BaseModel, Field

from . import retrieval
from .config import get_settings
from .embeddings import EmbeddingQuotaExhausted

# Kept small: tool results are sent back to the LLM, and free tiers limit tokens per minute.
MAX_RESULTS = 5
MAX_REASONS = 4
MAX_CHUNKS = 4
MAX_CHUNK_CHARS = 700


class ProfileArgs(BaseModel):
    grade_percent: float = Field(ge=0, le=100, description="Bachelor's grade as a percentage")
    background: str = Field(description="Degree background slug, e.g. computer-science, information-technology, electronics")
    budget_eur: float = Field(gt=0, description="Total budget in EUR for the whole course, living costs included")
    interests: list[str] = Field(default_factory=list, description="Subjects or career interests")
    english_test: Literal["Ielts", "Pte", "Toefl"] | None = None
    english_score: float | None = None
    work_years: float = Field(default=0, ge=0)


class SearchArgs(BaseModel):
    query: str = Field(description="What to look up in official university and government pages")
    site: str | None = Field(default=None, description="Optional domain filter, e.g. ucd.ie")


def summarise_recommendations(payload: dict) -> dict:
    items = []
    for r in payload.get("items", [])[:MAX_RESULTS]:
        c, cost = r["course"], r["cost"]
        items.append({
            "courseId": c["id"],
            "title": c["title"],
            "university": c["university"],
            "city": c["city"],
            "tags": r["tags"],
            "totalCostEur": cost["total"],
            "costBreakdownEur": {"tuition": cost["tuition"], "living": cost["living"], "oneOff": cost["oneOff"]},
            "reasons": r["reasons"][:MAX_REASONS],
            "sourceUrl": c["sourceUrl"],
            "verifiedAt": c["verifiedAt"],
        })
    return {"fitAvailable": payload.get("fitAvailable", False), "results": items}


@tool("get_recommendations", args_schema=ProfileArgs)
async def get_recommendations(**args) -> str:
    """Rank Irish master's courses for a student. Returns eligibility tags, total cost, reasons and official sources.
    Use this for any question about which courses fit, eligibility, or cost."""
    p = ProfileArgs(**args)
    body = {"profile": {
        "gradePercent": p.grade_percent,
        "background": p.background,
        "english": {"test": p.english_test, "score": p.english_score} if p.english_test and p.english_score else None,
        "workYears": p.work_years,
        "budgetEur": p.budget_eur,
        "interests": p.interests,
    }}
    try:
        async with httpx.AsyncClient(base_url=get_settings().compass_api_url, timeout=10) as client:
            resp = await client.post("/api/recommendations", json=body)
            resp.raise_for_status()
            return json.dumps(summarise_recommendations(resp.json()))
    except httpx.HTTPError as ex:
        return json.dumps({"error": f"Recommendations unavailable: {type(ex).__name__}"})


@tool("search_sources", args_schema=SearchArgs)
def search_sources(query: str, site: str | None = None) -> str:
    """Search official university and government pages. Returns text excerpts with source URLs and fetch dates.
    Use this for visa, scholarship, accommodation, course content or application questions."""
    try:
        chunks = retrieval.search(query, url_contains=site)
    except EmbeddingQuotaExhausted:
        return json.dumps({"results": [], "error": "Official-source search is temporarily unavailable. "
                           "Tell the student, answer only from what you already know with that caveat, "
                           "and suggest checking the university's own page."})
    if not chunks:
        return json.dumps({"results": [], "note": "No official source found."})
    return json.dumps({"results": [
        {"sourceUrl": c.source_url, "title": c.title, "fetchedAt": c.fetched_at,
         "similarity": c.similarity, "text": c.content[:MAX_CHUNK_CHARS]}
        for c in chunks[:MAX_CHUNKS]
    ]})


ALL_TOOLS = [get_recommendations, search_sources]
