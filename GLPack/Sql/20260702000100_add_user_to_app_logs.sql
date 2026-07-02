ALTER TABLE "AppLogs"
ADD COLUMN IF NOT EXISTS "UserId" integer;

ALTER TABLE "AppLogs"
ADD COLUMN IF NOT EXISTS "UserEmail" text NOT NULL DEFAULT '';

DO $$
BEGIN
    IF to_regclass('"__EFMigrationsHistory"') IS NOT NULL THEN
        INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
        SELECT '20260702000100_AddUserToAppLogs', '10.0.2'
        WHERE NOT EXISTS (
            SELECT 1
            FROM "__EFMigrationsHistory"
            WHERE "MigrationId" = '20260702000100_AddUserToAppLogs'
        );
    END IF;
END $$;
