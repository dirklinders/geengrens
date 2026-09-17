-- manual_015_location_code_locationid.sql
-- Unlock codes now point at map LOCATIONS instead of suspects:
-- LocationCodeModel.CharacterId (FK → Characters) was replaced by LocationId
-- (FK → Locations); entering a code unlocks the location itself (its dossier
-- on the Onderzoek tab and, when chat is enabled, the suspect found there).
-- Legacy databases still carry "LocationCodes"."CharacterId". This script:
--   1. drops the old FK to "Characters",
--   2. renames the column to "LocationId",
--   3. best-effort remaps the old character values to the map location where
--      that suspect is linked ("Locations"."CharacterId"); when several
--      locations share a suspect, one of them wins,
--   4. deletes codes that could not be remapped (no map location for that
--      suspect) — they are meaningless under the new model and must be
--      re-created in the admin UI. Their "TeamUnlocks" rows are removed
--      first, so this works regardless of cascade configuration,
--   5. enforces NOT NULL and adds the new FK to "Locations".
-- Guarded + idempotent: only runs when the legacy CharacterId column still
-- exists and LocationId does not, so it is a no-op on fresh databases where
-- the regenerated auto_LocationCodeModel.sql already created LocationId with
-- the correct FK.

DO $$
DECLARE
    fk_name TEXT;
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'LocationCodes' AND column_name = 'CharacterId'
    ) AND NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'LocationCodes' AND column_name = 'LocationId'
    ) THEN

        -- 1. Drop the old FK to "Characters". auto_LocationCodeModel.sql created
        --    it unnamed, so Postgres auto-named it; look it up dynamically.
        SELECT c.conname INTO fk_name
        FROM pg_constraint c
        JOIN pg_attribute a
            ON a.attrelid = c.conrelid AND a.attnum = ANY (c.conkey)
        WHERE c.conrelid = '"LocationCodes"'::regclass
          AND c.contype = 'f'
          AND a.attname = 'CharacterId'
        LIMIT 1;

        IF fk_name IS NOT NULL THEN
            EXECUTE format('ALTER TABLE "LocationCodes" DROP CONSTRAINT %I', fk_name);
        END IF;

        -- 2. Rename the column (old values: character ids).
        ALTER TABLE "LocationCodes" RENAME COLUMN "CharacterId" TO "LocationId";

        -- 3. Best-effort remap: old character id → the location that suspect
        --    sits at. Single statement, so every row is remapped against the
        --    pre-update values.
        UPDATE "LocationCodes" lc
        SET "LocationId" = l."Id"
        FROM "Locations" l
        WHERE l."CharacterId" = lc."LocationId";

        -- 4. Remove unlock records of unmappable codes, then the codes
        --    themselves (LocationId NULL or not pointing at a real location).
        DELETE FROM "TeamUnlocks"
        WHERE "LocationCodeId" IN (
            SELECT "Id" FROM "LocationCodes"
            WHERE "LocationId" IS NULL
               OR "LocationId" NOT IN (SELECT "Id" FROM "Locations")
        );

        DELETE FROM "LocationCodes"
        WHERE "LocationId" IS NULL
           OR "LocationId" NOT IN (SELECT "Id" FROM "Locations");

        -- 5. Enforce NOT NULL and add the new FK to "Locations".
        ALTER TABLE "LocationCodes" ALTER COLUMN "LocationId" SET NOT NULL;

        IF NOT EXISTS (
            SELECT 1 FROM pg_constraint
            WHERE conname = 'LocationCodes_LocationId_fkey'
              AND conrelid = '"LocationCodes"'::regclass
        ) THEN
            ALTER TABLE "LocationCodes"
                ADD CONSTRAINT "LocationCodes_LocationId_fkey"
                FOREIGN KEY ("LocationId") REFERENCES "Locations"("Id");
        END IF;
    END IF;
END $$;
