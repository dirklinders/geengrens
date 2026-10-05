-- manual_017_character_weapon_stop_keywords.sql
-- Adds the missing "StopKeywordWeapon" columns. manual_009 only added the
-- Alibi/Connection/Hint keyword columns; the weapon keyword was added to
-- CharacterModel and WeaponModel afterwards without a migration, so existing
-- databases (e.g. the production server) are missing the column and EF inserts
-- fail with SqlState 42703. Idempotent via IF NOT EXISTS.

ALTER TABLE "Characters" ADD COLUMN IF NOT EXISTS "StopKeywordWeapon" TEXT;
ALTER TABLE "Weapons"    ADD COLUMN IF NOT EXISTS "StopKeywordWeapon" TEXT;
