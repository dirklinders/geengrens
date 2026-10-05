-- manual_016_logigram_entry_entityid.sql
-- Logigram auto-sync from game content:
-- LogigramEntrys gains a nullable "EntityId" linking an entry to its source
-- entity (CharacterModel.Id / WeaponModel.Id / LocationModel.Id — the category
-- key determines which). LogigramSyncManager upserts entries by
-- (LogigramCategoryId, EntityId), so entry ids — and with them the team marks
-- (FK ON DELETE CASCADE, manual_012) — survive repeated syncs. A partial
-- unique index prevents duplicate entity links. Legacy/manual entries keep
-- "EntityId" NULL; the sync adopts them when their name matches a source
-- entity and removes them otherwise.

-- ── 1. Column ─────────────────────────────────────────────────
ALTER TABLE "LogigramEntrys" ADD COLUMN IF NOT EXISTS "EntityId" INTEGER NULL;

-- ── 2. One entity link per (category, entity) ─────────────────
DO $do$
BEGIN
    IF to_regclass('"LogigramEntrys"') IS NOT NULL THEN
        -- Remove duplicate links first so the index creation cannot fail
        -- (lowest Id wins; the extras cascade their marks away).
        DELETE FROM "LogigramEntrys" a
        USING "LogigramEntrys" b
        WHERE a."Id" > b."Id"
          AND a."LogigramCategoryId" = b."LogigramCategoryId"
          AND a."EntityId" IS NOT NULL
          AND a."EntityId" = b."EntityId";

        CREATE UNIQUE INDEX IF NOT EXISTS "uq_logigramentrys_category_entity"
            ON "LogigramEntrys" ("LogigramCategoryId", "EntityId")
            WHERE "EntityId" IS NOT NULL;
    END IF;
END $do$;
