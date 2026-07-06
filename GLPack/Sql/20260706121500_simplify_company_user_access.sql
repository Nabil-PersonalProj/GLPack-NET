ALTER TABLE company_user_access
DROP COLUMN IF EXISTS "IsEnabled";

ALTER TABLE company_user_access
DROP COLUMN IF EXISTS "AccessLevel";

ALTER TABLE company_user_access
DROP COLUMN IF EXISTS "UpdatedAtUtc";
