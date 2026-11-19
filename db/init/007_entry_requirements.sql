-- Entry requirements provenance.
-- backgrounds_open: the page names example disciplines and also accepts "related" ones, so a background
--   outside accepted_backgrounds is "not stated" rather than "not eligible".
-- entry_source_url: page the degree class / disciplines came from when it is not source_url.
-- grade_source_url: the university's official India conversion used to turn the degree class into
--   min_grade_percent.
ALTER TABLE courses ADD COLUMN IF NOT EXISTS backgrounds_open BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE courses ADD COLUMN IF NOT EXISTS entry_source_url TEXT;
ALTER TABLE courses ADD COLUMN IF NOT EXISTS grade_source_url TEXT;
