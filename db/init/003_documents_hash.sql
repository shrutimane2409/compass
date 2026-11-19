-- Lets the ingestion job skip unchanged pages and flag changed ones.
ALTER TABLE documents ADD COLUMN IF NOT EXISTS page_hash TEXT;
CREATE INDEX IF NOT EXISTS documents_source_idx ON documents(source_url);
