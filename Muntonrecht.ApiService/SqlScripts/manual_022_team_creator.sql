ALTER TABLE "Teams"
    ADD COLUMN IF NOT EXISTS "CreatedByUserId" text NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "uq_teams_one_player_creator"
    ON "Teams" ("CreatedByUserId")
    WHERE "CreatedByUserId" IS NOT NULL;
