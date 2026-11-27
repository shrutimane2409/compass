-- Optional accounts (Google sign-in), saved data, university rankings and scholarships.
-- Guests never touch these tables. Deleting a user removes everything they saved (ON DELETE CASCADE).

CREATE TABLE IF NOT EXISTS users (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    google_sub    TEXT NOT NULL UNIQUE,
    email         TEXT NOT NULL,
    name          TEXT,
    picture_url   TEXT,
    created_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Stored only after the student explicitly saves their profile (consent_at records when).
CREATE TABLE IF NOT EXISTS user_profiles (
    user_id     UUID PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    profile     JSONB NOT NULL,
    priority    TEXT NOT NULL DEFAULT 'Balanced',
    consent_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS saved_courses (
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    course_id   INT  NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
    note        TEXT CHECK (length(note) <= 500),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, course_id)
);

CREATE TABLE IF NOT EXISTS applications (
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    course_id   INT  NOT NULL REFERENCES courses(id) ON DELETE CASCADE,
    status      TEXT NOT NULL DEFAULT 'researching'
                CHECK (status IN ('researching','preparing','applied','offer','accepted','rejected')),
    documents   JSONB NOT NULL DEFAULT '{}',   -- {"sop":true,"lor1":false,...}
    notes       TEXT CHECK (length(notes) <= 2000),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, course_id)
);

CREATE TABLE IF NOT EXISTS checklist_progress (
    user_id     UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    item_id     TEXT NOT NULL CHECK (item_id ~ '^[a-z0-9-]{1,40}$'),
    done_at     TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (user_id, item_id)
);

-- Published league-table positions, shown as information. rank_low orders bands ("201-250" -> 201).
CREATE TABLE IF NOT EXISTS university_rankings (
    university_id  INT  NOT NULL REFERENCES universities(id) ON DELETE CASCADE,
    source         TEXT NOT NULL CHECK (source IN ('QS','THE')),
    edition        INT  NOT NULL,
    scope          TEXT NOT NULL DEFAULT 'overall',
    rank_text      TEXT NOT NULL,
    rank_low       INT  NOT NULL,
    source_url     TEXT NOT NULL,
    verified_at    DATE NOT NULL,
    PRIMARY KEY (university_id, source, edition, scope)
);

CREATE TABLE IF NOT EXISTS scholarships (
    id                 SERIAL PRIMARY KEY,
    slug               TEXT NOT NULL UNIQUE,
    name               TEXT NOT NULL,
    provider           TEXT NOT NULL,
    university_id      INT REFERENCES universities(id) ON DELETE CASCADE,   -- NULL = any Irish university
    amount_text        TEXT NOT NULL,
    max_value_eur      NUMERIC(10,2),
    fields             TEXT[] NOT NULL DEFAULT '{}',                        -- empty = any course
    min_grade_percent  NUMERIC(5,2),
    requires_offer     BOOLEAN NOT NULL DEFAULT true,
    automatic          BOOLEAN NOT NULL DEFAULT false,
    competitive        BOOLEAN NOT NULL DEFAULT false,
    nationality        TEXT NOT NULL DEFAULT 'non-eu' CHECK (nationality IN ('non-eu','india')),
    how_to_apply       TEXT NOT NULL,
    deadline_text      TEXT NOT NULL,
    source_url         TEXT NOT NULL,
    verified_at        DATE NOT NULL
);

ALTER TABLE usage_events DROP CONSTRAINT IF EXISTS usage_events_event_check;
ALTER TABLE usage_events ADD CONSTRAINT usage_events_event_check
    CHECK (event IN ('search','chat','report','helpful','signin','save','track'));
