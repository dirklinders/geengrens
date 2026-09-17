-- Ensure the 'admin' role exists in AspNetRoles, then assign it to dirk@dirklinders.nl
DO $$
DECLARE
    v_role_id TEXT;
    v_user_id TEXT;
BEGIN
    -- 1. Insert the admin role if it doesn't exist yet
    SELECT "Id" INTO v_role_id FROM "AspNetRoles" WHERE "NormalizedName" = 'ADMIN';

    IF v_role_id IS NULL THEN
        v_role_id := gen_random_uuid()::TEXT;
        INSERT INTO "AspNetRoles" ("Id", "Name", "NormalizedName", "ConcurrencyStamp")
        VALUES (v_role_id, 'admin', 'ADMIN', gen_random_uuid()::TEXT);
    END IF;

    -- 2. Find the user by email
    SELECT "Id" INTO v_user_id FROM "AspNetUsers" WHERE "NormalizedEmail" = 'DIRK@DIRKLINDERS.NL';

    IF v_user_id IS NULL THEN
        RAISE NOTICE 'User dirk@dirklinders.nl not found — skipping role assignment. Run again after first login.';
    ELSE
        -- 3. Assign the admin role if not already assigned
        IF NOT EXISTS (
            SELECT 1 FROM "AspNetUserRoles"
            WHERE "UserId" = v_user_id AND "RoleId" = v_role_id
        ) THEN
            INSERT INTO "AspNetUserRoles" ("UserId", "RoleId")
            VALUES (v_user_id, v_role_id);
            RAISE NOTICE 'Admin role assigned to dirk@dirklinders.nl';
        ELSE
            RAISE NOTICE 'dirk@dirklinders.nl already has the admin role';
        END IF;
    END IF;
END $$;
