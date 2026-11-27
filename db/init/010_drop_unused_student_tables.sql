-- Early design stored student profiles and shortlists. Compass now keeps no profile data by design
-- (no sign-up; profiles are only used in-request), so these tables were never written to.
DROP TABLE IF EXISTS shortlist_items;
DROP TABLE IF EXISTS students;
