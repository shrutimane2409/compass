-- Which academic year a fee applies to, and whether cost rows are derived (e.g. a range midpoint).
ALTER TABLE courses       ADD COLUMN IF NOT EXISTS fee_academic_year TEXT;
ALTER TABLE living_costs  ADD COLUMN IF NOT EXISTS is_estimate BOOLEAN NOT NULL DEFAULT FALSE;
ALTER TABLE one_off_costs ADD COLUMN IF NOT EXISTS is_estimate BOOLEAN NOT NULL DEFAULT FALSE;
