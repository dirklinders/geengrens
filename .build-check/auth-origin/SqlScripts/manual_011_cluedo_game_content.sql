-- manual_011_cluedo_game_content.sql
-- Cluedo game content + logigram:
-- 1. GameSettings: admin-editable intro (detective telegram) and rules content
--    (empty = GameDefaults fallback on the API side).
-- 2. Locations: content discriminator + flexible admin-authored JSON content
--    (ContentType: '' | 'interview' | 'search_picture').
-- 3. TeamProgresss: per-team seen timestamps driving the /intro → /rules → /game flow.
-- 4. Logigram tables (LogigramCategorys, LogigramEntrys, LogigramClues,
--    TeamLogigramMarks) are created by the auto_*.sql scripts generated at startup;
--    this script only re-adds their FKs with ON DELETE CASCADE (manual_007
--    convention) and enforces one mark per (team, entry).

-- ── GameSettings: intro (telegram) + rules content ──────────
-- Empty values fall back to GameDefaults on the API side.
ALTER TABLE "GameSettings" ADD COLUMN IF NOT EXISTS "IntroTitle" TEXT NOT NULL DEFAULT '';
ALTER TABLE "GameSettings" ADD COLUMN IF NOT EXISTS "IntroBody"  TEXT NOT NULL DEFAULT '';
ALTER TABLE "GameSettings" ADD COLUMN IF NOT EXISTS "RulesTitle" TEXT NOT NULL DEFAULT '';
ALTER TABLE "GameSettings" ADD COLUMN IF NOT EXISTS "RulesBody"  TEXT NOT NULL DEFAULT '';

-- ── Locations: content type + admin-authored JSON content ───
ALTER TABLE "Locations" ADD COLUMN IF NOT EXISTS "ContentType" TEXT NOT NULL DEFAULT '';
ALTER TABLE "Locations" ADD COLUMN IF NOT EXISTS "ContentJson" TEXT NOT NULL DEFAULT '';

-- ── TeamProgresss: intro/rules seen timestamps ──────────────
ALTER TABLE "TeamProgresss" ADD COLUMN IF NOT EXISTS "IntroSeenAt" TIMESTAMP NULL;
ALTER TABLE "TeamProgresss" ADD COLUMN IF NOT EXISTS "RulesSeenAt" TIMESTAMP NULL;

-- ── TeamLogigramMarks.TeamId → Teams.Id ─────────────────────
-- (guarded: the table is created by auto_TeamLogigramMarkModel.sql at startup)
DO $do$
BEGIN
    IF to_regclass('"TeamLogigramMarks"') IS NOT NULL THEN
        ALTER TABLE "TeamLogigramMarks" DROP CONSTRAINT IF EXISTS "TeamLogigramMarks_TeamId_fkey";
        ALTER TABLE "TeamLogigramMarks" DROP CONSTRAINT IF EXISTS "fk_teamlogigrammarks_teamid";
        ALTER TABLE "TeamLogigramMarks"
            ADD CONSTRAINT "fk_teamlogigrammarks_teamid"
            FOREIGN KEY ("TeamId") REFERENCES "Teams"("Id") ON DELETE CASCADE;

        ALTER TABLE "TeamLogigramMarks" DROP CONSTRAINT IF EXISTS "TeamLogigramMarks_LogigramEntryId_fkey";
        ALTER TABLE "TeamLogigramMarks" DROP CONSTRAINT IF EXISTS "fk_teamlogigrammarks_logigramentryid";
        ALTER TABLE "TeamLogigramMarks"
            ADD CONSTRAINT "fk_teamlogigrammarks_logigramentryid"
            FOREIGN KEY ("LogigramEntryId") REFERENCES "LogigramEntrys"("Id") ON DELETE CASCADE;
    END IF;
END $do$;

-- ── LogigramEntrys.LogigramCategoryId → LogigramCategorys.Id ─
DO $do$
BEGIN
    IF to_regclass('"LogigramEntrys"') IS NOT NULL THEN
        ALTER TABLE "LogigramEntrys" DROP CONSTRAINT IF EXISTS "LogigramEntrys_LogigramCategoryId_fkey";
        ALTER TABLE "LogigramEntrys" DROP CONSTRAINT IF EXISTS "fk_logigramentrys_logigramcategoryid";
        ALTER TABLE "LogigramEntrys"
            ADD CONSTRAINT "fk_logigramentrys_logigramcategoryid"
            FOREIGN KEY ("LogigramCategoryId") REFERENCES "LogigramCategorys"("Id") ON DELETE CASCADE;
    END IF;
END $do$;

-- ── One mark per (team, entry) ───────────────────────────────
-- The API upserts marks per entry; the unique index guards against duplicates
-- (e.g. from concurrent saves). Existing duplicates are removed first so the
-- index creation cannot fail.
DO $do$
BEGIN
    IF to_regclass('"TeamLogigramMarks"') IS NOT NULL THEN
        DELETE FROM "TeamLogigramMarks" a
        USING "TeamLogigramMarks" b
        WHERE a."Id" > b."Id"
          AND a."TeamId" = b."TeamId"
          AND a."LogigramEntryId" = b."LogigramEntryId";

        CREATE UNIQUE INDEX IF NOT EXISTS "uq_teamlogigrammarks_team_entry"
            ON "TeamLogigramMarks" ("TeamId", "LogigramEntryId");
    END IF;
END $do$;
