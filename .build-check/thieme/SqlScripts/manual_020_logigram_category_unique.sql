-- Repair concurrent-sync duplicates without deleting any entries or marks.
-- Keep populated categories first, then use the sync's ordering.
-- The DO statement makes cleanup and index creation atomic and retryable.
DO $do$
BEGIN
    LOCK TABLE "LogigramCategorys" IN SHARE ROW EXCLUSIVE MODE;
    LOCK TABLE "LogigramEntrys" IN SHARE ROW EXCLUSIVE MODE;
    WITH ranked AS (
        SELECT c."Id", ROW_NUMBER() OVER (
            PARTITION BY c."Key"
            ORDER BY EXISTS (
                SELECT 1 FROM "LogigramEntrys" e
                WHERE e."LogigramCategoryId" = c."Id"
            ) DESC, c."SortOrder", c."Id"
        ) AS position
        FROM "LogigramCategorys" c
    )
    DELETE FROM "LogigramCategorys" c USING ranked r
    WHERE c."Id" = r."Id" AND r.position > 1
      AND NOT EXISTS (
          SELECT 1 FROM "LogigramEntrys" e
          WHERE e."LogigramCategoryId" = c."Id"
      );
    IF EXISTS (
        SELECT 1 FROM "LogigramCategorys"
        GROUP BY "Key" HAVING COUNT(*) > 1
    ) THEN
        RAISE EXCEPTION 'Duplicate populated logigram categories require a data-preserving merge; no categories were removed.';
    END IF;
    CREATE UNIQUE INDEX IF NOT EXISTS "uq_logigramcategorys_key"
        ON "LogigramCategorys" ("Key");
END $do$;
