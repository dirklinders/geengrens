-- Existing tables are not updated by auto_TeamProgressModel.sql: it only
-- uses CREATE TABLE IF NOT EXISTS and is recorded once in applied_scripts.
-- Add the assignment/deduction and accusation fields introduced in the model.
-- Nullable columns preserve existing progress without inventing assignments.
-- Inline references match the generated schema when adding missing columns;
-- IF NOT EXISTS makes this safe on fresh databases and when retried.
ALTER TABLE "TeamProgresss"
    ADD COLUMN IF NOT EXISTS "LocationId" INTEGER REFERENCES "Locations"("Id"),
    ADD COLUMN IF NOT EXISTS "WeaponId" INTEGER REFERENCES "Weapons"("Id"),
    ADD COLUMN IF NOT EXISTS "TipLocationId" INTEGER,
    ADD COLUMN IF NOT EXISTS "TipWeaponId" INTEGER;
