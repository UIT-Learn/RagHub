-- Spike 0a: PostgreSQL 17 + pgvector round-trip
-- PostgreSQL version : 17.10
-- pgvector version   : 0.8.2
-- Docker image       : pgvector/pgvector:pg17
--
-- Run:
--   docker exec raghub-postgres psql -U postgres -d raghub -f db/spike/spike_0a_vector.sql
--
-- FINDINGS:
--   CREATE EXTENSION vector            -- WORKS
--   vector(N) column type              -- WORKS: stores 4-byte float per dimension
--   INSERT '[x, y, ...]'::vector       -- WORKS: implicit cast from string literal
--   <=>  cosine distance operator      -- WORKS: returns 0 for identical, 1 for orthogonal
--   <->  L2 distance operator          -- available (not tested here, not needed for POC)
--   <#>  negative dot product operator -- available (not tested here)
--   tsvector GENERATED ALWAYS AS ...   -- WORKS: auto-maintained from content column
--   GIN index on tsvector              -- WORKS
--   content_tsv @@ to_tsquery(...)     -- WORKS: returns matching rows with ts_rank scores
--
-- DECISION: pgvector <=> is the dense retrieval operator (Phase 3a).
--           tsvector/GIN is the sparse retrieval leg (Phase 3b hybrid).
--           Both work natively — no application-layer workaround needed.

-- ── Prerequisites ──────────────────────────────────────────────────────────
CREATE EXTENSION IF NOT EXISTS vector;

-- ── Part 1: Vector distance ─────────────────────────────────────────────────
DROP TABLE IF EXISTS spike_vec;

CREATE TABLE spike_vec (
    id   INT  PRIMARY KEY,
    name TEXT NOT NULL,
    v    vector(4) NOT NULL
);

-- id=1: [1,0,0,0] — the query vector itself   → cosine dist = 0
-- id=2: [0.9,0.1,0,0] — very close            → cosine dist ≈ 0.006
-- id=3: [0,1,0,0] — orthogonal                → cosine dist = 1
INSERT INTO spike_vec VALUES
    (1, 'identical',  '[1.0, 0.0, 0.0, 0.0]'),
    (2, 'very_close', '[0.9, 0.1, 0.0, 0.0]'),
    (3, 'orthogonal', '[0.0, 1.0, 0.0, 0.0]');

-- Expected order: identical (0), very_close (~0.006), orthogonal (1)
SELECT id, name, (v <=> '[1.0, 0.0, 0.0, 0.0]') AS cosine_dist
FROM spike_vec
ORDER BY cosine_dist ASC;

-- ── Part 2: Full-text search (sparse leg of hybrid retrieval) ───────────────
DROP TABLE IF EXISTS spike_fts;

CREATE TABLE spike_fts (
    id          INT  PRIMARY KEY,
    content     TEXT NOT NULL,
    content_tsv tsvector GENERATED ALWAYS AS (to_tsvector('english', content)) STORED
);

CREATE INDEX ON spike_fts USING GIN (content_tsv);

INSERT INTO spike_fts(id, content) VALUES
    (1, 'Annual leave policy employees entitled twelve days'),
    (2, 'POST /api/orders create new order endpoint'),
    (3, 'Architecture overview deployment troubleshooting guide');

-- Query for 'annual leave' — should return only row 1
SELECT id, content, ts_rank(content_tsv, query) AS rank
FROM spike_fts, to_tsquery('english', 'annual & leave') query
WHERE content_tsv @@ query
ORDER BY rank DESC;

-- ── Cleanup ─────────────────────────────────────────────────────────────────
DROP TABLE spike_fts;
DROP TABLE spike_vec;
