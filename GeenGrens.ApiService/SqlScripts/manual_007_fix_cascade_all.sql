-- manual_007_fix_cascade_all.sql
-- Idempotent: drops FK constraints by every possible name they could have
-- (original auto-generated OR names from manual_005/006) then re-adds with CASCADE.
-- Uses plain DDL only — no DO blocks — so Npgsql executes it cleanly.

-- ── TeamUnlocks.TeamId → Teams.Id ────────────────────────────
ALTER TABLE "TeamUnlocks" DROP CONSTRAINT IF EXISTS "TeamUnlocks_TeamId_fkey";
ALTER TABLE "TeamUnlocks" DROP CONSTRAINT IF EXISTS "fk_teamunlocks_teamid";
ALTER TABLE "TeamUnlocks"
    ADD CONSTRAINT "fk_teamunlocks_teamid"
    FOREIGN KEY ("TeamId") REFERENCES "Teams"("Id") ON DELETE CASCADE;

-- ── TeamUnlocks.LocationCodeId → LocationCodes.Id ────────────
ALTER TABLE "TeamUnlocks" DROP CONSTRAINT IF EXISTS "TeamUnlocks_LocationCodeId_fkey";
ALTER TABLE "TeamUnlocks" DROP CONSTRAINT IF EXISTS "fk_teamunlocks_locationcodeid";
ALTER TABLE "TeamUnlocks"
    ADD CONSTRAINT "fk_teamunlocks_locationcodeid"
    FOREIGN KEY ("LocationCodeId") REFERENCES "LocationCodes"("Id") ON DELETE CASCADE;

-- ── TeamProgresss.TeamId → Teams.Id ──────────────────────────
ALTER TABLE "TeamProgresss" DROP CONSTRAINT IF EXISTS "TeamProgresss_TeamId_fkey";
ALTER TABLE "TeamProgresss" DROP CONSTRAINT IF EXISTS "fk_teamprogresss_teamid";
ALTER TABLE "TeamProgresss"
    ADD CONSTRAINT "fk_teamprogresss_teamid"
    FOREIGN KEY ("TeamId") REFERENCES "Teams"("Id") ON DELETE CASCADE;

-- ── Chats.CharacterId → Characters.Id ────────────────────────
ALTER TABLE "Chats" DROP CONSTRAINT IF EXISTS "Chats_CharacterId_fkey";
ALTER TABLE "Chats" DROP CONSTRAINT IF EXISTS "fk_chats_characterid";
ALTER TABLE "Chats"
    ADD CONSTRAINT "fk_chats_characterid"
    FOREIGN KEY ("CharacterId") REFERENCES "Characters"("Id") ON DELETE CASCADE;

-- ── LocationCodes.CharacterId → Characters.Id ────────────────
ALTER TABLE "LocationCodes" DROP CONSTRAINT IF EXISTS "LocationCodes_CharacterId_fkey";
ALTER TABLE "LocationCodes" DROP CONSTRAINT IF EXISTS "fk_locationcodes_characterid";
ALTER TABLE "LocationCodes"
    ADD CONSTRAINT "fk_locationcodes_characterid"
    FOREIGN KEY ("CharacterId") REFERENCES "Characters"("Id") ON DELETE CASCADE;

-- ── CharacterTag.CharacterId → Characters.Id ─────────────────
ALTER TABLE "CharacterTag" DROP CONSTRAINT IF EXISTS "CharacterTag_CharacterId_fkey";
ALTER TABLE "CharacterTag" DROP CONSTRAINT IF EXISTS "fk_charactertag_characterid";
ALTER TABLE "CharacterTag"
    ADD CONSTRAINT "fk_charactertag_characterid"
    FOREIGN KEY ("CharacterId") REFERENCES "Characters"("Id") ON DELETE CASCADE;

-- ── CharacterTag.TagId → Tags.Id ─────────────────────────────
ALTER TABLE "CharacterTag" DROP CONSTRAINT IF EXISTS "CharacterTag_TagId_fkey";
ALTER TABLE "CharacterTag" DROP CONSTRAINT IF EXISTS "fk_charactertag_tagid";
ALTER TABLE "CharacterTag"
    ADD CONSTRAINT "fk_charactertag_tagid"
    FOREIGN KEY ("TagId") REFERENCES "Tags"("Id") ON DELETE CASCADE;
