-- manual_014_location_characterid_nullable.sql
-- "Locations"."CharacterId" is an OPTIONAL foreign key: a map location may exist
-- without a linked suspect (admin form option "— Geen verdachte gekoppeld —").
-- LocationModel.CharacterId is now int?, so EF sends NULL on insert. Older
-- databases may still carry the legacy NOT NULL constraint on this column;
-- drop it. Guarded + idempotent: only alters when the column exists AND is
-- currently NOT NULL, so it runs harmlessly on databases that are already
-- nullable and on fresh databases (where auto_LocationModel.sql never added
-- a NOT NULL constraint).
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Locations'
          AND column_name = 'CharacterId'
          AND is_nullable = 'NO'
    ) THEN
        ALTER TABLE "Locations" ALTER COLUMN "CharacterId" DROP NOT NULL;
    END IF;
END $$;
