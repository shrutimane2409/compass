-- National College of Ireland (NCI): QQI-awarded, Dublin IFSC campus.
INSERT INTO universities (name, short_name, city, website)
VALUES ('National College of Ireland', 'NCI', 'Dublin', 'https://www.ncirl.ie')
ON CONFLICT (name) DO NOTHING;
