-- Add IsPlaytest flag to Teams table
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Teams' AND column_name = 'IsPlaytest'
    ) THEN
        ALTER TABLE "Teams" ADD COLUMN "IsPlaytest" BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;
END $$;
