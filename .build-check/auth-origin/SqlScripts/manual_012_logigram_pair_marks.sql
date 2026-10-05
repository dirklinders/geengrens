-- manual_012_logigram_pair_marks.sql
-- Logigram pair marks:
-- A 7×7×7 logigram needs pairwise cell marks (each unordered cross-category
-- pair appears in exactly one cell), but per-entry storage gives each entry a
-- single mark while it participates in ~12 independent cells. This script adds
-- an optional second entry reference to TeamLogigramMarks:
--   "LogigramEntryBId" IS NULL     → per-entry mark (row/column conclusion);
--   "LogigramEntryBId" IS NOT NULL → unordered pair mark for one grid cell,
--     normalized by the API: LogigramEntryId = min(idA, idB),
--     LogigramEntryBId = max(idA, idB).
-- 1. Add the nullable column.
-- 2. FK → LogigramEntrys with ON DELETE CASCADE (manual_007 convention).
-- 3. Replace the per-entry unique index with a functional one that treats a
--    NULL entryB as 0, so per-entry marks and pair marks coexist.

-- ── 1. Column ─────────────────────────────────────────────────
ALTER TABLE "TeamLogigramMarks" ADD COLUMN IF NOT EXISTS "LogigramEntryBId" INTEGER NULL;

-- ── 2. TeamLogigramMarks.LogigramEntryBId → LogigramEntrys.Id ─
DO $do$
BEGIN
    IF to_regclass('"TeamLogigramMarks"') IS NOT NULL AND to_regclass('"LogigramEntrys"') IS NOT NULL THEN
        ALTER TABLE "TeamLogigramMarks" DROP CONSTRAINT IF EXISTS "TeamLogigramMarks_LogigramEntryBId_fkey";
        ALTER TABLE "TeamLogigramMarks" DROP CONSTRAINT IF EXISTS "fk_teamlogigrammarks_logigramentrybid";
        ALTER TABLE "TeamLogigramMarks"
            ADD CONSTRAINT "fk_teamlogigrammarks_logigramentrybid"
            FOREIGN KEY ("LogigramEntryBId") REFERENCES "LogigramEntrys"("Id") ON DELETE CASCADE;
    END IF;
END $do$;

-- ── 3. Unique index handling NULL entryB ──────────────────────
DO $do$
BEGIN
    IF to_regclass('"TeamLogigramMarks"') IS NOT NULL THEN
        -- Normalize legacy/unordered pair rows: entryA = min, entryB = max
        -- (both right-hand sides read the pre-update values).
        UPDATE "TeamLogigramMarks"
        SET "LogigramEntryId"  = LEAST("LogigramEntryId", "LogigramEntryBId"),
            "LogigramEntryBId" = GREATEST("LogigramEntryId", "LogigramEntryBId")
        WHERE "LogigramEntryBId" IS NOT NULL
          AND "LogigramEntryId" > "LogigramEntryBId";

        -- Drop the old per-entry unique index (guarded).
        DROP INDEX IF EXISTS "uq_teamlogigrammarks_team_entry";

        -- Remove duplicates on the new key so the index creation cannot fail.
        DELETE FROM "TeamLogigramMarks" a
        USING "TeamLogigramMarks" b
        WHERE a."Id" > b."Id"
          AND a."TeamId" = b."TeamId"
          AND a."LogigramEntryId" = b."LogigramEntryId"
          AND COALESCE(a."LogigramEntryBId", 0) = COALESCE(b."LogigramEntryBId", 0);

        CREATE UNIQUE INDEX IF NOT EXISTS "uq_teamlogigrammarks_team_entry_pair"
            ON "TeamLogigramMarks" ("TeamId", "LogigramEntryId", COALESCE("LogigramEntryBId", 0));
    END IF;
END $do$;
