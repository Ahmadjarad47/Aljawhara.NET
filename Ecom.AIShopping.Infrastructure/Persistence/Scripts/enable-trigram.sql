-- Optional typo-tolerant lexical candidate retrieval. Review and run explicitly on the existing database.
-- Exact scans are appropriate for a small catalog. Benchmark before adding another GIN index.
CREATE EXTENSION IF NOT EXISTS pg_trgm;
