-- Disable SemanticEnabled/IndexingEnabled before running. Embeddings can be rebuilt.
-- Do not DROP EXTENSION: other applications in this database may use it.
BEGIN;
ALTER TABLE ai_shopping."SearchDocuments" DROP COLUMN IF EXISTS "Embedding";
UPDATE ai_shopping."SearchDocuments" SET "EmbeddingModel" = NULL, "IndexedAtUtc" = NULL;
COMMIT;
