-- "Report an error" queue. Reviewed by a human against the official page before any data change.
CREATE TABLE IF NOT EXISTS course_feedback (
    id          BIGSERIAL PRIMARY KEY,
    course_id   INT NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
    field       TEXT NOT NULL CHECK (field IN ('fee','entry-requirements','english','deadline','living-costs','other')),
    message     TEXT NOT NULL CHECK (length(message) BETWEEN 1 AND 1000),
    page_url    TEXT,
    status      TEXT NOT NULL DEFAULT 'open' CHECK (status IN ('open','confirmed','rejected','fixed')),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    notified_at TIMESTAMPTZ,          -- when a maintainer was alerted (GitHub issue)
    resolved_at TIMESTAMPTZ
);
CREATE INDEX IF NOT EXISTS course_feedback_open_idx ON course_feedback(status, created_at) WHERE status = 'open';

-- Anonymous product analytics: no names, emails, IPs or profile values. session_id is a random id per browser tab.
CREATE TABLE IF NOT EXISTS usage_events (
    id          BIGSERIAL PRIMARY KEY,
    occurred_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    event       TEXT NOT NULL CHECK (event IN ('search','chat','report','helpful')),
    session_id  UUID,
    props       JSONB NOT NULL DEFAULT '{}'
);
CREATE INDEX IF NOT EXISTS usage_events_time_idx ON usage_events(occurred_at);
CREATE INDEX IF NOT EXISTS usage_events_event_time_idx ON usage_events(event, occurred_at);
