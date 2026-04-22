-- manual_006_cascade_character_tags.sql
-- Adds ON DELETE CASCADE to the CharacterTag join table so that
-- deleting a Character (or a Tag) automatically removes its join rows.

-- ── 1. CharacterTag.CharacterId → Characters.Id ──────────────
DO $$
DECLARE v_constraint TEXT;
BEGIN
    SELECT tc.constraint_name INTO v_constraint
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
       AND tc.table_schema    = kcu.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
      AND tc.table_name      = 'CharacterTag'
      AND kcu.column_name    = 'CharacterId'
    LIMIT 1;

    IF v_constraint IS NOT NULL THEN
        EXECUTE format('ALTER TABLE "CharacterTag" DROP CONSTRAINT %I', v_constraint);
        RAISE NOTICE 'Dropped FK: %', v_constraint;
    END IF;
END $$;

ALTER TABLE "CharacterTag"
    ADD CONSTRAINT "fk_charactertag_characterid"
    FOREIGN KEY ("CharacterId") REFERENCES "Characters"("Id") ON DELETE CASCADE;

-- ── 2. CharacterTag.TagId → Tags.Id ──────────────────────────
DO $$
DECLARE v_constraint TEXT;
BEGIN
    SELECT tc.constraint_name INTO v_constraint
    FROM information_schema.table_constraints tc
    JOIN information_schema.key_column_usage kcu
        ON tc.constraint_name = kcu.constraint_name
       AND tc.table_schema    = kcu.table_schema
    WHERE tc.constraint_type = 'FOREIGN KEY'
      AND tc.table_name      = 'CharacterTag'
      AND kcu.column_name    = 'TagId'
    LIMIT 1;

    IF v_constraint IS NOT NULL THEN
        EXECUTE format('ALTER TABLE "CharacterTag" DROP CONSTRAINT %I', v_constraint);
        RAISE NOTICE 'Dropped FK: %', v_constraint;
    END IF;
END $$;

ALTER TABLE "CharacterTag"
    ADD CONSTRAINT "fk_charactertag_tagid"
    FOREIGN KEY ("TagId") REFERENCES "Tags"("Id") ON DELETE CASCADE;
