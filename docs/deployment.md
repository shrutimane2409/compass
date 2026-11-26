# Deployment (free tiers, no card)

| Part | Host |
|---|---|
| Database | **Neon** (PostgreSQL + pgvector) |
| AI service + API | **Render** (two free Docker web services, defined in `render.yaml`) |
| Web | **Vercel** |
| Embeddings and LLM | **Gemini** (embeddings, fallback LLM) and **Groq** (primary LLM), free API keys |

Free-tier limits change; check each provider's current limits when you sign up.
Free services sleep when idle, so the first request after a quiet period is slow. Open the site a minute before a demo.

## 0. Keys you need

| Key | Where |
|---|---|
| Shared secret | `openssl rand -hex 32` in Terminal. Used as `INTERNAL_API_KEY` and `AiService__InternalKey` |
| `GEMINI_API_KEY` | aistudio.google.com → Get API key (required: embeddings) |
| `GROQ_API_KEY` | console.groq.com → API Keys |

## 1. Database: Neon

    for f in db/init/*.sql; do psql "$NEON_URL" -f "$f"; done

    cd ai && source .venv/bin/activate
    export DATABASE_URL="$NEON_URL" GEMINI_API_KEY="..."
    python -m scripts.load_courses ../data/courses.csv
    python -m scripts.load_costs
    python -m scripts.ingest ../data/sources.txt

Add `DATABASE_URL` and `GEMINI_API_KEY` as GitHub Actions secrets for the weekly source refresh.

## 2. AI service and API: Render

1. render.com → sign up with GitHub → **New → Blueprint** → select the `compass` repo. Render reads `render.yaml`
   and creates `compass-ai` and `compass-api`.
2. Enter the secrets it asks for:

   **compass-ai**

   | Key | Value |
   |---|---|
   | `DATABASE_URL` | Neon connection string |
   | `INTERNAL_API_KEY` | the shared secret |
   | `GEMINI_API_KEY`, `GROQ_API_KEY` | your keys |
   | `COMPASS_API_URL` | `https://compass-api.onrender.com` (the API's URL) |

   **compass-api**

   | Key | Value |
   |---|---|
   | `ConnectionStrings__Compass` | `Host=<neon-host>;Database=neondb;Username=neondb_owner;Password=<password>;SSL Mode=Require` |
   | `AiService__BaseUrl` | `https://compass-ai.onrender.com` (the AI service's URL) |
   | `AiService__InternalKey` | the shared secret |
   | `Cors__Origins__0` | your Vercel URL (add after step 3) |

   Render shows each service's real URL on its page; use those if they differ from the names above.
3. Check `https://<compass-ai-url>/health` and `https://<compass-api-url>/health/ready`.

## 3. Web: Vercel

1. vercel.com → sign up with GitHub → **Add New → Project** → import `compass` → **Root Directory** `web`.
2. Environment variable `VITE_API_BASE` = the compass-api URL (no trailing slash). Deploy.
3. Put the Vercel URL into `Cors__Origins__0` on compass-api and redeploy it.

## 4. Smoke test

    cd ai && python -m evals.golden_profiles https://<compass-api-url>

Open the Vercel URL, run a search, drag the budget slider, and ask the chat a question.
