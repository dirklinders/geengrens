-- manual_010_cluedo_overhaul.sql
-- Cluedo overhaul:
-- 1. Ensures the GameSettings table exists (SqlScriptGenerator also creates it as
--    auto_GameSettingModel.sql; this is defensive so seeding always works) and
--    seeds the single solution/settings row.
-- 2. Drops notebook remnants (NotebookLocation on Teams, IsNotebookUnlocked on
--    TeamProgresss) and the free-text TipMotive — the accusation is now
--    strictly suspect + weapon + location.

CREATE TABLE IF NOT EXISTS "GameSettings" (
    "Id" INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "MurdererCharacterId" INTEGER NOT NULL DEFAULT 0,
    "MurderWeaponId" INTEGER NOT NULL DEFAULT 0,
    "MurderLocationId" INTEGER NOT NULL DEFAULT 0,
    "SpeluitlegTitle" TEXT NOT NULL DEFAULT '',
    "SpeluitlegBackstory" TEXT NOT NULL DEFAULT '',
    "SpeluitlegRules" TEXT NOT NULL DEFAULT ''
);

-- Seed the single settings row (empty solution — configure via admin settings page;
-- empty speluitleg texts fall back to GameDefaults on the API side).
INSERT INTO "GameSettings" ("MurdererCharacterId", "MurderWeaponId", "MurderLocationId", "SpeluitlegTitle", "SpeluitlegBackstory", "SpeluitlegRules")
SELECT 0, 0, 0, '', '', ''
WHERE NOT EXISTS (SELECT 1 FROM "GameSettings");

-- Notebook concept removed
ALTER TABLE "Teams" DROP COLUMN IF EXISTS "NotebookLocation";
ALTER TABLE "TeamProgresss" DROP COLUMN IF EXISTS "IsNotebookUnlocked";

-- Motive dropped from the accusation (strict Cluedo: suspect + weapon + location)
ALTER TABLE "TeamProgresss" DROP COLUMN IF EXISTS "TipMotive";
