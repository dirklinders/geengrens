ALTER TABLE "TeamProgresss"
    ADD COLUMN IF NOT EXISTS "AllLocationsUnlocked" boolean NOT NULL DEFAULT FALSE;
