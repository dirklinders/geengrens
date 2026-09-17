-- manual_005_cascade_deletes.sql
-- Drops and re-adds FK constraints with ON DELETE CASCADE so that
-- deleting a parent record (Team, LocationCode, Character) automatically
-- removes dependent rows instead of throwing a FK violation.

-- ── 1. TeamUnlocks.TeamId → Teams.Id ─────────────────────────
DO $$
DECLARE v_constraint TEXT;
BEGIN
    SELECT tc.constraint_name INTO v_constraint
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
       AND tc.table_schema    = kcu.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
      AND tc.table_name      = 'TeamUnlocks'
      AND kcu.column_name    = 'TeamId'
    LIMIT 1;

    IF v_constraint IS NOT NULL THEN
        EXECUTE format('ALTER TABLE "TeamUnlocks" DROP CONSTRAINT %I', v_constraint);
        RAISE NOTICE 'Dropped FK: %', v_constraint;
    END IF;
END $$;

ALTER TABLE "TeamUnlocks"
    ADD CONSTRAINT "fk_teamunlocks_teamid"
    FOREIGN KEY ("TeamId") REFERENCES "Teams"("Id") ON DELETE CASCADE;

-- ── 2. TeamUnlocks.LocationCodeId → LocationCodes.Id ─────────
DO $$
DECLARE v_constraint TEXT;
BEGIN
    SELECT tc.constraint_name INTO v_constraint
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
       AND tc.table_schema    = kcu.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
      AND tc.table_name      = 'TeamUnlocks'
      AND kcu.column_name    = 'LocationCodeId'
    LIMIT 1;

    IF v_constraint IS NOT NULL THEN
        EXECUTE format('ALTER TABLE "TeamUnlocks" DROP CONSTRAINT %I', v_constraint);
        RAISE NOTICE 'Dropped FK: %', v_constraint;
    END IF;
END $$;

ALTER TABLE "TeamUnlocks"
    ADD CONSTRAINT "fk_teamunlocks_locationcodeid"
    FOREIGN KEY ("LocationCodeId") REFERENCES "LocationCodes"("Id") ON DELETE CASCADE;

-- ── 3. TeamProgresss.TeamId → Teams.Id ───────────────────────
DO $$
DECLARE v_constraint TEXT;
BEGIN
    SELECT tc.constraint_name INTO v_constraint
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
       AND tc.table_schema    = kcu.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
      AND tc.table_name      = 'TeamProgresss'
      AND kcu.column_name    = 'TeamId'
    LIMIT 1;

    IF v_constraint IS NOT NULL THEN
        EXECUTE format('ALTER TABLE "TeamProgresss" DROP CONSTRAINT %I', v_constraint);
        RAISE NOTICE 'Dropped FK: %', v_constraint;
    END IF;
END $$;

ALTER TABLE "TeamProgresss"
    ADD CONSTRAINT "fk_teamprogresss_teamid"
    FOREIGN KEY ("TeamId") REFERENCES "Teams"("Id") ON DELETE CASCADE;

-- ── 4. Chats.CharacterId → Characters.Id ─────────────────────
DO $$
DECLARE v_constraint TEXT;
BEGIN
    SELECT tc.constraint_name INTO v_constraint
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
       AND tc.table_schema    = kcu.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
      AND tc.table_name      = 'Chats'
      AND kcu.column_name    = 'CharacterId'
    LIMIT 1;

    IF v_constraint IS NOT NULL THEN
        EXECUTE format('ALTER TABLE "Chats" DROP CONSTRAINT %I', v_constraint);
        RAISE NOTICE 'Dropped FK: %', v_constraint;
    END IF;
END $$;

ALTER TABLE "Chats"
    ADD CONSTRAINT "fk_chats_characterid"
    FOREIGN KEY ("CharacterId") REFERENCES "Characters"("Id") ON DELETE CASCADE;

-- ── 5. LocationCodes.CharacterId → Characters.Id ─────────────
DO $$
DECLARE v_constraint TEXT;
BEGIN
    SELECT tc.constraint_name INTO v_constraint
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
       AND tc.table_schema    = kcu.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
      AND tc.table_name      = 'LocationCodes'
      AND kcu.column_name    = 'CharacterId'
    LIMIT 1;

    IF v_constraint IS NOT NULL THEN
        EXECUTE format('ALTER TABLE "LocationCodes" DROP CONSTRAINT %I', v_constraint);
        RAISE NOTICE 'Dropped FK: %', v_constraint;
    END IF;
END $$;

ALTER TABLE "LocationCodes"
    ADD CONSTRAINT "fk_locationcodes_characterid"
    FOREIGN KEY ("CharacterId") REFERENCES "Characters"("Id") ON DELETE CASCADE;
