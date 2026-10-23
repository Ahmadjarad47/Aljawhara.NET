-- Explicit opt-in, after the EF AI migration. Run with an extension-capable deployment role.
-- This is intentionally separate: lexical-only installations do not require pgvector.
BEGIN;
CREATE EXTENSION IF NOT EXISTS vector;
ALTER TABLE ai_shopping."SearchDocuments" ADD COLUMN IF NOT EXISTS "Embedding" vector(1536);
-- Exact cosine-distance scan first. Benchmark before considering HNSW/IVFFlat.
COMMIT;
