-- Switch embeddings to Gemini (768 dimensions). Existing 384-dimension vectors are dropped:
-- re-run scripts.load_courses and scripts.ingest afterwards to re-embed.
DROP INDEX IF EXISTS courses_embedding_idx;
DROP INDEX IF EXISTS documents_embedding_idx;
ALTER TABLE courses   ALTER COLUMN embedding TYPE vector(768) USING NULL::vector(768);
DELETE FROM documents;
ALTER TABLE documents ALTER COLUMN embedding TYPE vector(768) USING NULL::vector(768);
CREATE INDEX courses_embedding_idx   ON courses   USING hnsw (embedding vector_cosine_ops);
CREATE INDEX documents_embedding_idx ON documents USING hnsw (embedding vector_cosine_ops);
