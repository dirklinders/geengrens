-- Store each team's working name for the anonymous suspect and preserve the
-- exact submitted label for the admin's final-accusation overview.
ALTER TABLE "TeamProgresss"
    ADD COLUMN IF NOT EXISTS "UnknownSuspectName" TEXT NULL,
    ADD COLUMN IF NOT EXISTS "TipSuspectDisplayName" TEXT NULL;
