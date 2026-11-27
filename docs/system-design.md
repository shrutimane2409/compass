# Compass · System Design

## 1. Goals

Compass recommends Irish taught master's courses to Indian students based on their profile, budget and goals.
It explains every recommendation and links every fact to an official source.

### Functional
- Conversational onboarding that builds a student profile
- Shortlist tagged Eligible / Reach / Good fit / Affordable, with reasons
- Real total cost: tuition + living for the course duration + one-off costs
- What-if simulator: change budget, field or grades and see the shortlist update
- Questions answered from official pages, with citations
- Personal application roadmap with deadlines

### Non-functional
| Concern | Target |
|---|---|
| Shortlist / what-if latency | p95 < 200 ms server side |
| Chat time to first token | < 2 s, streamed |
| Agent answer | < 8 s, streamed step by step |
| Degradation | Shortlist, costs and simulator work when every LLM is unavailable |
| Cost | Free tiers only |
| Trust | Every fact has a source URL and verification date |

## 2. Core principle: deterministic facts, probabilistic understanding

| Deterministic (.NET, unit tested) | Probabilistic (Python) |
|---|---|
| Eligibility | Interpreting interests and goals |
| Cost and affordability | Course-fit scoring (embeddings) |
| Freshness flags | Grounded answers (RAG) |
| Ranking formula | Counsellor agent |

The AI can reorder courses through the fit score. It can never change eligibility or cost.

## 3. Architecture

```
            React SPA (CDN)
                  │  HTTPS · SSE
        ┌─────────▼──────────┐
        │  Compass API (.NET) │  public entry point
        │  rate limiting · validation · rules engine · in-memory catalog cache
        └──┬───────────────┬──┘
           │ SQL           │ HTTP (resilient) + SSE proxy
           │        ┌──────▼──────────────┐
           │        │ Compass AI (Python) │  embeddings · RAG · LangGraph agent
           │        └──┬───────────┬──────┘
           │           │           │  agent tools call the API's rules endpoints (read only)
     ┌─────▼───────────▼─┐   ┌─────▼─────────────┐
     │ PostgreSQL+pgvector│   │ LLM: Groq ⇄ Gemini │  primary / fallback
     └────────────────────┘   └────────────────────┘
       ▲
       └── weekly ingestion job: fetch official pages → hash → re-chunk/embed changed pages → flag course for re-verification
```

### Components
- **Web**: React + TypeScript. Calls only the Compass API.
- **Compass API**: ASP.NET Core minimal APIs. Owns the rules engine, the catalog cache, rate limiting and the public contract. Proxies chat streams to the AI service so there is one origin, one rate limiter and one place for validation.
- **Compass AI**: FastAPI. Owns embeddings, retrieval and the agent. Stateless.
- **PostgreSQL + pgvector**: structured facts in tables; course descriptions and document chunks carry 768-dimension embeddings (Gemini) with HNSW indexes.
- **Ingestion job**: scheduled CI job. Never auto-updates fees or requirements; it flags them for a human to verify.

## 4. Key request paths

### 4.1 Shortlist and what-if (hot path, no LLM)
1. `POST /api/recommendations` with the profile.
2. Catalog (courses, city costs, one-off costs) comes from an in-memory cache (TTL 10 min, invalidated on data load). About 80 rows, so no database round trip.
3. Fit scores: looked up in the cache by a hash of the student's interests; on a miss, one call to `AI /fit` (one embedding + pgvector search). If the AI service fails, the API falls back to a neutral score and returns `fitAvailable: false`.
4. Rules engine evaluates every course in memory (about 10 ms).

A what-if change (budget, grade) reuses the cached fit scores, so it never leaves the API process.

### 4.2 Chat and agent (streamed)
1. `POST /api/chat`, rate limited per IP.
2. API opens a streaming request to `AI /chat` and relays server-sent events to the browser as they arrive.
3. The agent uses at most 4 tool calls per turn. Tools: `get_recommendations`, `search_sources`, `get_costs`, `build_roadmap`.
4. Primary model on Groq for speed; falls back to Gemini on rate limit or error. If both fail, the client shows "chat is busy" and the rest of the app keeps working.

### 4.3 Grounded answers (RAG)
1. Embed the question (Gemini embeddings, query task type; repeated questions hit an in-process cache).
2. Retrieve top chunks from `documents` with pgvector, filtered by university when known.
3. Prompt instructs the model to answer only from the retrieved chunks and cite `source_url`; otherwise say it does not know.
4. Retrieved text is treated as data, never as instructions.

## 5. Latency budget (shortlist)

| Step | Budget |
|---|---|
| Network + validation | 20 ms |
| Catalog cache hit | < 1 ms |
| Fit cache hit / miss | < 1 ms / ~300 ms (one embedding call) |
| Rules engine | ~10 ms |
| Serialization | ~5 ms |

## 6. Reliability
- Resilient HTTP to the AI service: timeout, retry with jitter, circuit breaker.
- Graceful degradation at every AI boundary.
- Stateless services: any instance serves any request.
- Liveness (`/health/live`) and readiness (`/health/ready`, checks the database) probes.

## 7. Security and abuse
- Per-IP rate limits: general API 60/min, chat 10/min. Protects the free LLM quota.
- Agent tools are read only. No tool can write data or call arbitrary URLs.
- Guests are anonymous. Accounts are optional: Google Identity Services issues an ID token in the browser; the API
  verifies it against Google's published keys (issuer, audience, expiry, verified email) and issues its own
  HS256 session token (30 days) used as a bearer token, since the web app and API are on different sites.
- Every account query is scoped to the user id in the token; deleting the user cascades to all saved data.
  Students can export everything stored about them as JSON.
- Secrets come from environment variables; nothing sensitive is committed.
- Behind a managed proxy, the client IP is read from `X-Forwarded-For`.

## 8. Data quality
- Every course row stores `source_url`, `verified_at` and `is_estimate`.
- Rows older than 180 days are tagged `StaleData` in results.
- The ingestion job hashes each official page; a changed hash flags the course for review.

## 9. Observability
- OpenTelemetry traces across Web → API → AI → LLM, exported over OTLP when configured.
- Structured logs with trace IDs.
- Per-request LLM latency, provider and token counts.

## 10. Testing and evaluation
- Unit tests for every rule (eligibility, cost, affordability, ranking, freshness).
- Golden profiles: fixed student profiles with expected shortlist properties.
- RAG evaluation set: answer faithfulness and citation correctness.
- Agent scenarios: tool choice, step limit, refusal when sources are missing.

## 11. Deployment (free tier)
- Web on Vercel; API and AI as Docker web services on Render (`render.yaml`); PostgreSQL + pgvector on Neon.
- Embeddings use Gemini's API rather than a local model: free hosts offer about 512 MB of memory, too little for a local model.
  Trade-off: embeddings depend on an external rate limit, offset by caching query vectors and fit scores.
- Known trade-off: scale-to-zero means a cold start after idle periods. Mitigated with a scheduled warm-up before demos.

## 12. Future work
- Distributed cache when running more than one API instance.
- More destination countries.
- Accounts with saved shortlists.
