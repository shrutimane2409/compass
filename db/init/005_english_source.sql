-- Where the English requirement came from when the course page defers to a university-wide page.
-- NULL means min_ielts / min_pte / min_toefl were taken from source_url.
ALTER TABLE courses ADD COLUMN IF NOT EXISTS english_source_url TEXT;
