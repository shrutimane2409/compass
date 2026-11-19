# Deployment (free tiers)

| Part | Host | Why |
|---|---|---|
| Database | **Neon** (PostgreSQL + pgvector) | Managed Postgres with the `vector` extension |
| AI service | **Hugging Face Spaces** (Docker) | Enough memory for the local embedding model |
| API | **Render** (Docker web service) | Runs the .NET container from `api/Dockerfile` |
| Web | **Vercel** | Static hosting on a CDN |

Free-tier limits change; check each provider's current limits when you sign up.
Free services sleep when idle, so the first request after a quiet period is slow (cold start).
Open the site a minute before a demo.

## 0. Generate a shared secret

    openssl rand -hex 32

This is `INTERNAL_API_KEY` (AI service) and `AiService__InternalKey` (API). It stops anyone
calling the public AI service directly and using up the free LLM quota.

## 1. Database: Neon

1. Create a project and copy the connection string.
2. Run the migrations in order:

       for f in db/init/*.sql; do psql "$NEON_URL" -f "$f"; done

3. Load the data from your machine:

       cd ai && source .venv/bin/activate
       export DATABASE_URL="$NEON_URL"
       python -m scripts.load_courses ../data/courses.csv
       python -m scripts.load_costs
       python -m scripts.ingest ../data/sources.txt

4. Add `DATABASE_URL` as a GitHub Actions secret so the weekly source refresh runs against Neon.

## 2. AI service: Hugging Face Spaces

1. New Space → SDK **Docker** → blank template.
2. Push the contents of `ai/` to the Space repository, plus a `README.md` at its root starting with:

       ---
       title: Compass AI
       sdk: docker
       app_port: 8000
       ---

3. Settings → Variables and secrets:
   `DATABASE_URL`, `INTERNAL_API_KEY`, `GROQ_API_KEY`, `GEMINI_API_KEY`,
   `COMPASS_API_URL` (the Render URL from step 3).
4. Check `https://<space-subdomain>.hf.space/health` returns `{"status":"ok"}`.

## 3. API: Render

1. New → Web Service → connect the GitHub repo → **Root directory** `api`, **Runtime** Docker.
2. Environment variables:

   | Key | Value |
   |---|---|
   | `ConnectionStrings__Compass` | `Host=<neon-host>;Database=<db>;Username=<user>;Password=<password>;SSL Mode=Require` |
   | `AiService__BaseUrl` | `https://<space-subdomain>.hf.space` |
   | `AiService__InternalKey` | the shared secret |
   | `Cors__Origins__0` | your Vercel URL, e.g. `https://compass-xyz.vercel.app` |
   | `ForwardedHeaders__Enabled` | `true` |

3. Health check path: `/health/ready`. The container listens on port 8080.

## 4. Web: Vercel

1. Import the repo → **Root directory** `web` → framework Vite.
2. Environment variable: `VITE_API_BASE` = the Render URL (no trailing slash).
3. Deploy, then put the Vercel URL into `Cors__Origins__0` on Render.

## 5. Smoke test

    curl https://<render-url>/health/ready
    cd ai && python -m evals.golden_profiles https://<render-url>

Then open the Vercel URL, run a search, drag the budget slider and ask the chat a question.
