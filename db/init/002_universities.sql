-- Universities only. Course, fee and cost rows are added from data/*.csv
-- after each value is checked against the official source.
INSERT INTO universities (name, short_name, city, website) VALUES
 ('University College Dublin',            'UCD',       'Dublin',   'https://www.ucd.ie'),
 ('Trinity College Dublin',               'TCD',       'Dublin',   'https://www.tcd.ie'),
 ('Dublin City University',               'DCU',       'Dublin',   'https://www.dcu.ie'),
 ('Technological University Dublin',      'TU Dublin', 'Dublin',   'https://www.tudublin.ie'),
 ('University of Galway',                 'Galway',    'Galway',   'https://www.universityofgalway.ie'),
 ('University College Cork',              'UCC',       'Cork',     'https://www.ucc.ie'),
 ('University of Limerick',               'UL',        'Limerick', 'https://www.ul.ie'),
 ('Maynooth University',                  'Maynooth',  'Maynooth', 'https://www.maynoothuniversity.ie');
