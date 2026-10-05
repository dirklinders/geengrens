-- manual_004_remove_story.sql
-- Removes the StoryId foreign key from "Characters" and drops the "Storys" table.
-- Idempotent: each step checks before acting.

-- 1. Drop FK constraint on Characters.StoryId (if it exists)
DO $$
DECLARE
    v_constraint TEXT;
BEGIN
    SELECT tc.constraint_name
      INTO v_constraint
      FROM information_schema.table_constraints tc
      JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
       AND tc.table_schema    = kcu.table_schema
     WHERE tc.constraint_type = 'FOREIGN KEY'
       AND tc.table_name      = 'Characters'
       AND kcu.column_name    = 'StoryId'
     LIMIT 1;

    IF v_constraint IS NOT NULL THEN
        EXECUTE format('ALTER TABLE "Characters" DROP CONSTRAINT %I', v_constraint);
        RAISE NOTICE 'Dropped FK constraint: %', v_constraint;
    ELSE
        RAISE NOTICE 'No FK constraint on Characters.StoryId — skipping.';
    END IF;
END
$$;

-- 2. Drop the StoryId column from Characters (if it exists)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
          FROM information_schema.columns
         WHERE table_name  = 'Characters'
           AND column_name = 'StoryId'
    ) THEN
        ALTER TABLE "Characters" DROP COLUMN "StoryId";
        RAISE NOTICE 'Dropped column Characters.StoryId.';
    ELSE
        RAISE NOTICE 'Column Characters.StoryId does not exist — skipping.';
    END IF;
END
$$;

-- 3. Drop the Storys table (if it exists)
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
          FROM information_schema.tables
         WHERE table_name = 'Storys'
    ) THEN
        DROP TABLE "Storys";
        RAISE NOTICE 'Dropped table Storys.';
    ELSE
        RAISE NOTICE 'Table Storys does not exist — skipping.';
    END IF;
END
$$;
