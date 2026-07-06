DELETE FROM company_user_access access
USING app_user users
WHERE access."UserId" = users."Id"
  AND users."IsAdmin" = true;
