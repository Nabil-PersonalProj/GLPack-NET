CREATE TABLE IF NOT EXISTS company_user_access (
    "CompanyId" integer NOT NULL,
    "UserId" integer NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT "PK_company_user_access" PRIMARY KEY ("CompanyId", "UserId"),
    CONSTRAINT "FK_company_user_access_company"
        FOREIGN KEY ("CompanyId") REFERENCES company ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_company_user_access_app_user"
        FOREIGN KEY ("UserId") REFERENCES app_user ("Id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_company_user_access_user_id"
ON company_user_access ("UserId");
