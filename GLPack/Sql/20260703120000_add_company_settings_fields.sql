ALTER TABLE company
ADD COLUMN IF NOT EXISTS "Currency" character varying(3) NOT NULL DEFAULT 'SGD';

ALTER TABLE company
ADD COLUMN IF NOT EXISTS "FinancialYearEnd" character varying(5) NOT NULL DEFAULT '12-31';
