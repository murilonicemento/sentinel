-- Aligns an alert_orchestrator database created by the old 2-schema.sql with the EF Core baseline migration
-- 20261025161922_InitialBaseline and records the migration as applied. Run once, before deploying a version that
-- relies on migrations. Safe to re-run: every step checks the current state.
--
--   psql "<connection string>" -v ON_ERROR_STOP=1 -f alert_orchestrator-InitialBaseline-align.sql

BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'alert_windows_pkey') THEN
        ALTER TABLE alert_windows RENAME CONSTRAINT alert_windows_pkey TO "PK_alert_windows";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'alert_signals_pkey') THEN
        ALTER TABLE alert_signals RENAME CONSTRAINT alert_signals_pkey TO "PK_alert_signals";
    END IF;
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'alert_signals_alert_window_id_fkey') THEN
        ALTER TABLE alert_signals RENAME CONSTRAINT alert_signals_alert_window_id_fkey
            TO "FK_alert_signals_alert_windows_alert_window_id";
    END IF;
    -- The old script declared UNIQUE (event_id) as a constraint; the model uses a unique index with the same name.
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'alert_signals_event_id_key') THEN
        ALTER TABLE alert_signals DROP CONSTRAINT alert_signals_event_id_key;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS alert_signals_event_id_key ON alert_signals (event_id);
-- Redundant with the unique index above.
DROP INDEX IF EXISTS idx_alert_signals_event_id;
CREATE INDEX IF NOT EXISTS "IX_alert_signals_alert_window_id" ON alert_signals (alert_window_id);

UPDATE alert_signals SET metadata = '{}'::jsonb WHERE metadata IS NULL;
ALTER TABLE alert_signals ALTER COLUMN metadata SET NOT NULL;

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261025161922_InitialBaseline', '9.0.20')
ON CONFLICT ("MigrationId") DO NOTHING;

COMMIT;
