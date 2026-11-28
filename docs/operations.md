# Operating Compass

## What is measured (and what is not)

`usage_events` holds anonymous product events only:

| event     | props                                                              |
|-----------|--------------------------------------------------------------------|
| `search`  | `ms`, `priority`, `results`, `eligible`, `fitAvailable`, `interests` (count only) |
| `chat`    | none                                                               |
| `report`  | `field`                                                            |
| `helpful` | `helpful` (bool), `context` (`shortlist` or `chat`)               |

`session_id` is a random UUID per browser tab (sessionStorage). No names, emails, IP addresses, grades, budgets
or chat text are stored. Page views come from Vercel Web Analytics (cookieless).

Events are queued in memory (bounded, drop-oldest) and batch-inserted by `UsageWriter`. If the database is down,
events are dropped and the request still succeeds.

## Quick numbers

`GET /api/admin/stats` with header `X-Admin-Key: <Admin__Key>` returns sessions, searches, chats, helpful votes,
search latency p50/p95 and open reports. Returns 404 if `Admin__Key` is not set.

## SQL for deeper questions

```sql
-- Weekly active sessions and searches
SELECT date_trunc('week', occurred_at) AS week,
       count(DISTINCT session_id) AS sessions,
       count(*) FILTER (WHERE event = 'search') AS searches,
       count(*) FILTER (WHERE event = 'chat') AS chats
FROM usage_events GROUP BY 1 ORDER BY 1 DESC;

-- Activation: share of sessions that searched and then used chat
SELECT round(100.0 * count(*) FILTER (WHERE chatted) / nullif(count(*), 0), 1) AS pct_chat_after_search
FROM (SELECT session_id, bool_or(event = 'chat') AS chatted
      FROM usage_events WHERE session_id IS NOT NULL GROUP BY session_id
      HAVING bool_or(event = 'search')) s;

-- Returning sessions (searched on 2+ separate days in the same tab session is rare; use as a lower bound)
SELECT count(*) FROM (SELECT session_id FROM usage_events WHERE event = 'search'
                      GROUP BY session_id HAVING count(DISTINCT occurred_at::date) > 1) r;

-- Helpfulness by surface
SELECT props->>'context' AS surface,
       count(*) FILTER (WHERE (props->>'helpful')::boolean) AS yes,
       count(*) FILTER (WHERE NOT (props->>'helpful')::boolean) AS no
FROM usage_events WHERE event = 'helpful' GROUP BY 1;

-- How often students hit "no eligible course" (a signal the catalogue is too narrow)
SELECT count(*) FILTER (WHERE (props->>'eligible')::int = 0) AS zero_eligible, count(*) AS searches
FROM usage_events WHERE event = 'search';

-- Search latency percentiles, last 7 days
SELECT percentile_cont(ARRAY[0.5, 0.95]) WITHIN GROUP (ORDER BY (props->>'ms')::float)
FROM usage_events WHERE event = 'search' AND occurred_at > now() - interval '7 days';

-- Open data reports
SELECT f.id, c.title, f.field, f.message, f.created_at
FROM course_feedback f JOIN courses c ON c.id = f.course_id
WHERE f.status = 'open' ORDER BY f.created_at;
```

## Synthetic traffic (load and monitoring tests)

`python -m scripts.simulate_traffic <api> --students 30` (or the manual **Simulate traffic** workflow) plays
randomised student sessions: searches with what-if edits, a few chats (capped), helpful votes. It prints p50/p95
latency, status codes, 429s and AI degradation. Every request sends `X-Synthetic: 1`; those events are tagged
`synthetic` and excluded from `/api/admin/stats` (shown separately as `syntheticEvents`). It never submits error
reports. Clean up with:

```sql
DELETE FROM usage_events WHERE (props->>'synthetic')::boolean;
```

## Resolving a data report

1. The `Feedback triage` workflow opens a GitHub issue (label `data-report`).
2. Check the official page linked in the issue.
3. If the report is right: fix `data/courses.csv`, run `python -m scripts.load_courses`, then
   `UPDATE course_feedback SET status = 'fixed', resolved_at = now() WHERE id = <id>;`
4. If not: set `status = 'rejected'` and close the issue with a note.

## Scheduled workflows

| Workflow              | When                         | Fails when                                    |
|-----------------------|------------------------------|-----------------------------------------------|
| `uptime.yml`          | every 10 min, 08-22 UTC      | `/health/ready` is down after retries         |
| `prod-eval.yml`       | nightly 02:43 UTC            | a golden-profile invariant breaks in prod     |
| `feedback-triage.yml` | every 6 h                    | an issue can't be created                     |
| `refresh-sources.yml` | Mondays 04:17 UTC            | ingestion fails                               |
| `data-freshness.yml`  | daily 05:23 UTC              | never; findings go to the `data-freshness` issue |

GitHub emails the repo owner when a scheduled run fails. Render's free tier gives 750 instance hours a month per
workspace; the uptime schedule uses about 430 for the API.

## Data freshness

`python -m scripts.freshness` (from `ai/`) audits `data/`: facts verified more than 180 days ago (the API's
"Check dates" window) and broken source links are **errors**; fee years behind the upcoming intake, older ranking
editions and sites that block bots are **warnings**. The daily workflow keeps one open issue labelled
`data-freshness` up to date and closes it once there are no errors. Fix by re-checking the official page, updating
the CSV value and `verified_at`, and reloading with the matching `scripts.load_*`. Use `--no-links` for an offline run.

## Rankings and scholarships

Both live in CSV with a source URL and check date per row: `data/rankings.csv`, `data/scholarships.csv`.
Load or refresh with `python -m scripts.load_extras` (from `ai/`). QS publishes in June and THE in late September or
October: add the new edition's rows, keep the old ones (the API shows the latest edition per source).
Scholarship pages are also in `data/sources.txt`, so the weekly refresh flags when they change.

## Accounts

Sign-in needs two Render env vars on `compass-api`: `Auth__GoogleClientId` (OAuth web client id, with the web
app's origin in *Authorized JavaScript origins*) and `Auth__JwtSecret` (32+ random characters). Without them the site
works in guest mode and the sign-in button is hidden. Rotating `Auth__JwtSecret` signs everyone out.
