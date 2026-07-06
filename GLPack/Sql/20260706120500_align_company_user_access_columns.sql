DO $$
BEGIN
    IF to_regclass('public.company_user_access') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'company_user_access'
              AND column_name = 'company_id'
        ) AND NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'company_user_access'
              AND column_name = 'CompanyId'
        ) THEN
            ALTER TABLE company_user_access RENAME COLUMN company_id TO "CompanyId";
        END IF;

        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'company_user_access'
              AND column_name = 'user_id'
        ) AND NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'company_user_access'
              AND column_name = 'UserId'
        ) THEN
            ALTER TABLE company_user_access RENAME COLUMN user_id TO "UserId";
        END IF;

        IF EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'company_user_access'
              AND column_name = 'created_at_utc'
        ) AND NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'company_user_access'
              AND column_name = 'CreatedAtUtc'
        ) THEN
            ALTER TABLE company_user_access RENAME COLUMN created_at_utc TO "CreatedAtUtc";
        END IF;
    END IF;
END $$;

ALTER TABLE company_user_access
ALTER COLUMN "CreatedAtUtc" SET DEFAULT now();

UPDATE company_user_access
SET "CreatedAtUtc" = now()
WHERE "CreatedAtUtc" IS NULL;

ALTER TABLE company_user_access
ALTER COLUMN "CreatedAtUtc" SET NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_company_user_access_user_id"
ON company_user_access ("UserId");
