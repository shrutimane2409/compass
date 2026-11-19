-- Compass schema: structured, verifiable facts live in tables;
-- free text that the AI searches gets an embedding.
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE universities (
    id          SERIAL PRIMARY KEY,
    name        TEXT NOT NULL UNIQUE,
    short_name  TEXT NOT NULL,
    city        TEXT NOT NULL,
    country     CHAR(2) NOT NULL DEFAULT 'IE',
    website     TEXT NOT NULL
);

CREATE TABLE courses (
    id                     SERIAL PRIMARY KEY,
    university_id          INT NOT NULL REFERENCES universities(id),
    title                  TEXT NOT NULL,
    award                  TEXT NOT NULL,             -- MSc, MEng ...
    field                  TEXT NOT NULL,             -- computer-science, data-science, ai, software-engineering
    duration_months        INT NOT NULL CHECK (duration_months > 0),
    intake_month           INT NOT NULL CHECK (intake_month BETWEEN 1 AND 12),
    fee_non_eu_eur         NUMERIC(10,2) NOT NULL CHECK (fee_non_eu_eur >= 0),
    application_deadline   DATE,
    min_grade_percent      NUMERIC(5,2),              -- Indian degree, normalised to %
    accepted_backgrounds   TEXT[] NOT NULL DEFAULT '{}',
    min_ielts              NUMERIC(3,1),
    min_pte                INT,
    min_toefl              INT,
    min_work_years         NUMERIC(3,1) NOT NULL DEFAULT 0,
    description            TEXT NOT NULL DEFAULT '',  -- overview + modules, used for semantic matching
    career_outcomes        TEXT NOT NULL DEFAULT '',
    source_url             TEXT NOT NULL,             -- official page the facts came from
    verified_at            DATE NOT NULL,             -- when a human last checked source_url
    is_estimate            BOOLEAN NOT NULL DEFAULT FALSE,
    embedding              VECTOR(384),
    UNIQUE (university_id, title)
);
CREATE INDEX courses_field_idx ON courses(field);
CREATE INDEX courses_embedding_idx ON courses USING hnsw (embedding vector_cosine_ops);

-- Monthly living costs per city, by category (rent, food, transport, other)
CREATE TABLE living_costs (
    city         TEXT NOT NULL,
    category     TEXT NOT NULL,
    monthly_eur  NUMERIC(8,2) NOT NULL CHECK (monthly_eur >= 0),
    source_url   TEXT NOT NULL,
    verified_at  DATE NOT NULL,
    PRIMARY KEY (city, category)
);

-- One-off costs per country: visa fee, insurance, registration, deposit ...
CREATE TABLE one_off_costs (
    country      CHAR(2) NOT NULL,
    code         TEXT NOT NULL,
    description  TEXT NOT NULL,
    amount_eur   NUMERIC(8,2) NOT NULL CHECK (amount_eur >= 0),
    source_url   TEXT NOT NULL,
    verified_at  DATE NOT NULL,
    PRIMARY KEY (country, code)
);

-- RAG corpus: chunks of official pages
CREATE TABLE documents (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    source_url   TEXT NOT NULL,
    title        TEXT NOT NULL,
    chunk_index  INT NOT NULL,
    content      TEXT NOT NULL,
    fetched_at   TIMESTAMPTZ NOT NULL DEFAULT now(),
    embedding    VECTOR(384),
    UNIQUE (source_url, chunk_index)
);
CREATE INDEX documents_embedding_idx ON documents USING hnsw (embedding vector_cosine_ops);

CREATE TABLE students (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    created_at  TIMESTAMPTZ NOT NULL DEFAULT now(),
    profile     JSONB NOT NULL
);

CREATE TABLE shortlist_items (
    student_id  UUID NOT NULL REFERENCES students(id) ON DELETE CASCADE,
    course_id   INT  NOT NULL REFERENCES courses(id),
    added_at    TIMESTAMPTZ NOT NULL DEFAULT now(),
    PRIMARY KEY (student_id, course_id)
);
