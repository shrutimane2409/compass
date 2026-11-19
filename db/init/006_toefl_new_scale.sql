-- TOEFL iBT moved to a 1-6 band scale on 21 January 2026. min_toefl keeps the old 0-120 minimum;
-- min_toefl_new holds the overall minimum on the new scale when the official page states one.
ALTER TABLE courses ADD COLUMN IF NOT EXISTS min_toefl_new NUMERIC(2,1)
    CHECK (min_toefl_new BETWEEN 1 AND 6);
