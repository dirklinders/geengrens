-- Team-scoped search-picture discoveries. Hotspot IDs are supplied by the
-- admin-authored location JSON, therefore they are stored as text rather than
-- as a foreign key. The unique index makes reveal requests idempotent.
CREATE TABLE IF NOT EXISTS "TeamSearchPictureReveals" (
    "Id" INTEGER GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    "TeamId" INTEGER NOT NULL,
    "LocationId" INTEGER NOT NULL,
    "HotspotId" TEXT NOT NULL,
    "RevealedAt" TIMESTAMP NOT NULL DEFAULT NOW()
);

DO $do$
BEGIN
    ALTER TABLE "TeamSearchPictureReveals"
        DROP CONSTRAINT IF EXISTS "TeamSearchPictureReveals_TeamId_fkey";
    ALTER TABLE "TeamSearchPictureReveals"
        DROP CONSTRAINT IF EXISTS "fk_teamsearchpicturereveals_teamid";
    ALTER TABLE "TeamSearchPictureReveals"
        ADD CONSTRAINT "fk_teamsearchpicturereveals_teamid"
        FOREIGN KEY ("TeamId") REFERENCES "Teams"("Id") ON DELETE CASCADE;

    ALTER TABLE "TeamSearchPictureReveals"
        DROP CONSTRAINT IF EXISTS "TeamSearchPictureReveals_LocationId_fkey";
    ALTER TABLE "TeamSearchPictureReveals"
        DROP CONSTRAINT IF EXISTS "fk_teamsearchpicturereveals_locationid";
    ALTER TABLE "TeamSearchPictureReveals"
        ADD CONSTRAINT "fk_teamsearchpicturereveals_locationid"
        FOREIGN KEY ("LocationId") REFERENCES "Locations"("Id") ON DELETE CASCADE;
END $do$;

CREATE UNIQUE INDEX IF NOT EXISTS "uq_teamsearchpicturereveals_team_location_hotspot"
    ON "TeamSearchPictureReveals" ("TeamId", "LocationId", "HotspotId");
