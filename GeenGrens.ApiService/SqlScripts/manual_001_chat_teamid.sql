-- Add nullable TeamId FK to Chats table for per-team chat history scoping
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'Chats' AND column_name = 'TeamId'
    ) THEN
        ALTER TABLE "Chats" ADD COLUMN "TeamId" INTEGER;
        ALTER TABLE "Chats" ADD CONSTRAINT "fk_chats_teamid"
            FOREIGN KEY ("TeamId") REFERENCES "Teams"("Id") ON DELETE SET NULL;
    END IF;
END $$;
