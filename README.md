# Compass

An unbiased AI study-abroad counsellor. It recommends countries, universities and courses
from a student's profile, budget and goals, and explains every recommendation with official sources.

**MVP scope:** India → Ireland · taught master's · CS, Data Science, AI, Software Engineering.

## Core rule

| Decided by deterministic code (.NET, unit-tested) | Done by AI (Python) |
|---|---|
| Eligibility (grade, background, English, experience) | Understanding interests and goals |
| Total cost and budget fit | Course-fit scoring with embeddings |
| Data freshness flags | Answering questions with citations (RAG) |
| | Counsellor agent: interview → shortlist → roadmap |

The AI can reorder courses. It can never change whether a student is eligible or what a course costs.

See [docs/system-design.md](docs/system-design.md) for architecture, latency budgets and trade-offs,
and [docs/deployment.md](docs/deployment.md) for the free-tier deployment.

## Structure

    api/   ASP.NET Core API + domain rules engine + xUnit tests
    ai/    FastAPI service: embeddings, RAG, LangGraph agent
    web/   React + TypeScript frontend
    db/    PostgreSQL + pgvector schema and seed
    data/  Curated course and cost data (official sources only)
    docs/  System design

## Run locally

    cp .env.example .env
    docker compose up -d                 # PostgreSQL + pgvector on :5433

    cd api && dotnet test                # rules engine tests
    dotnet run --project src/Compass.Api # API on http://localhost:5080

    cd ai && python3 -m venv .venv && source .venv/bin/activate
    pip install -r requirements-dev.txt && pytest
    cp .env.example .env                 # add free GROQ_API_KEY / GEMINI_API_KEY
    uvicorn app.main:app --reload --port 8000

    python -m scripts.load_courses ../data/courses.csv
    python -m scripts.load_costs
    python -m scripts.ingest ../data/sources.txt

    cd web && npm install && npm run dev  # http://localhost:5173

## Run everything in Docker

    docker compose --profile app up -d --build      # web on http://localhost:3000
    docker compose --profile seed run --rm seed     # load courses, costs and official pages

## Evaluation

    cd ai
    python -m evals.rag_eval            # retrieval hit@6 and MRR over 12 questions
    python -m evals.golden_profiles     # shortlist invariants against the running API

## Roadmap

1. ✅ Schema, rules engine (eligibility, cost, affordability, freshness), tests, API
1b. ✅ API hardening: catalog cache, fit-score cache, rate limiting, resilience, SSE chat proxy, health checks, tracing
2. ✅ 24 verified courses across 8 universities + living and one-off costs (entry requirements: next pass)
3. ✅ Local embeddings + pgvector course-fit scoring (`/fit`)
4. ✅ Retrieval over official pages + ingestion job (`/search`, `scripts/ingest.py`)
5. ✅ LangGraph counsellor agent with Groq → Gemini fallback, streamed (`/chat`)
6. ✅ React UI: profile, ranked shortlist with reasons and sources, cost breakdown (EUR + INR), what-if budget slider, compare (up to 3), streaming chat
7. ✅ Full Docker Compose, CI (tests on every push), weekly source refresh, evaluation suite
8. ⬜ Entry requirements data pass · free-tier deployment · demo video

Not legal or immigration advice. Always confirm on the official university and government sites.
