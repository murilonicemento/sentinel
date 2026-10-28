-- INGESTION SERVICE DATABASE SCHEMA

\c
ingestion;

CREATE TABLE tenant
(
    id         uuid PRIMARY KEY      DEFAULT gen_random_uuid(),
    name       varchar(100) NOT NULL,
    is_active  boolean      NOT NULL DEFAULT true,
    created_at timestamptz  NOT NULL DEFAULT now()
);

CREATE TABLE data_source
(
    id                   uuid PRIMARY KEY,
    name                 varchar(100) NOT NULL,
    endpoint             varchar(100),
    data_source_type     varchar(20)  NOT NULL,
    measurement_type     varchar(20),
    collection_frequency varchar(20)  NOT NULL,
    tenant_id            uuid NULL,
    created_at           timestamptz  not null default now()
);

CREATE TABLE event_type_permission
(
    id             uuid PRIMARY KEY,
    data_source_id uuid        NOT NULL REFERENCES data_source (id),
    event_domain   varchar(20) NOT NULL, -- "Climatic" ou "Disaster"
    event_type     varchar(50) NOT NULL, -- "TemperatureAnomaly", "WindGust", "Wildfire", etc
    UNIQUE (data_source_id, event_domain, event_type)
);

CREATE TABLE data_collection
(
    id             uuid PRIMARY KEY,
    data_source_id uuid        NOT NULL REFERENCES data_source (id),
    collected_at   timestamptz NOT NULL,
    payload        jsonb       NOT NULL,
    tenant_id      uuid NULL,
    created_at     timestamptz not null default now(),
);

CREATE TABLE sensor_sample
(
    id                 uuid PRIMARY KEY,
    data_collection_id uuid             NOT NULL REFERENCES data_collection (id),
    sensor_value       double precision NOT NULL,
    unit               varchar(20)      NOT NULL,
    latitude           double precision NOT NULL,
    longitude          double precision NOT NULL,
    recorded_at        timestamptz      NOT NULL
);

CREATE TABLE outbox
(
    id           uuid PRIMARY KEY,
    aggregate_id uuid NULL,
    outbox_type  text    not null,
    payload      jsonb   not null,
    processed    boolean not null default false,
    processed_at timestamptz NULL
);

CREATE INDEX idx_outbox_processed ON outbox (processed) WHERE processed = false;

-- RISK CATALOG SERVICE DATABASE SCHEMA

\c risk_catalog;

-- Generated from the EF Core migrations (services/risk-catalog/src/RiskCatalog.Infrastructure/Persistence/Migrations):
--   dotnet ef migrations script --idempotent --project services/risk-catalog/src/RiskCatalog.Infrastructure \
--     --startup-project services/risk-catalog/src/RiskCatalog.Infrastructure
-- The previous version of this script did not match the EF model (no users table, regional_parameter without the
-- center/bounds columns): databases created from it must be recreated.

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE TABLE event_type (
        id uuid NOT NULL,
        code varchar(50) NOT NULL,
        name varchar(100) NOT NULL,
        description varchar(255) NOT NULL,
        is_active boolean NOT NULL DEFAULT TRUE,
        CONSTRAINT "PK_event_type" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE TABLE regional_parameter (
        id uuid NOT NULL,
        adjustment_factor double precision NOT NULL,
        description varchar(255) NOT NULL,
        "CenterLatitude" double precision,
        "CenterLongitude" double precision,
        "RegionBoundsJson" text,
        "CoverageRadiusKm" double precision,
        CONSTRAINT "PK_regional_parameter" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE TABLE severity (
        id uuid NOT NULL,
        level integer NOT NULL,
        description varchar(255) NOT NULL,
        CONSTRAINT "PK_severity" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE TABLE users (
        "Id" uuid NOT NULL DEFAULT (gen_random_uuid()),
        "Username" character varying(100) NOT NULL,
        "Email" character varying(100) NOT NULL,
        "FullName" character varying(200) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
        "UpdatedAt" timestamp with time zone,
        "IsActive" boolean NOT NULL,
        "Role" character varying(50) NOT NULL DEFAULT 'User',
        CONSTRAINT "PK_users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE TABLE idf_curve (
        id uuid NOT NULL,
        event_type_id uuid NOT NULL,
        duration_minutes integer NOT NULL,
        intensity double precision NOT NULL,
        return_period_years integer NOT NULL,
        version integer NOT NULL,
        CONSTRAINT "PK_idf_curve" PRIMARY KEY (id),
        CONSTRAINT "FK_idf_curve_event_type_event_type_id" FOREIGN KEY (event_type_id) REFERENCES event_type (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE TABLE risk_matrix (
        id uuid NOT NULL,
        event_type_id uuid NOT NULL,
        severity_level integer NOT NULL,
        risk_level integer NOT NULL,
        version integer NOT NULL,
        CONSTRAINT "PK_risk_matrix" PRIMARY KEY (id),
        CONSTRAINT "FK_risk_matrix_event_type_event_type_id" FOREIGN KEY (event_type_id) REFERENCES event_type (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE TABLE severity_criterion (
        id uuid NOT NULL,
        event_type_id uuid NOT NULL,
        severity_id uuid NOT NULL,
        min_value double precision NOT NULL,
        max_value double precision NOT NULL,
        unit varchar(50) NOT NULL,
        version integer NOT NULL,
        CONSTRAINT "PK_severity_criterion" PRIMARY KEY (id),
        CONSTRAINT "FK_severity_criterion_event_type_event_type_id" FOREIGN KEY (event_type_id) REFERENCES event_type (id) ON DELETE CASCADE,
        CONSTRAINT "FK_severity_criterion_severity_severity_id" FOREIGN KEY (severity_id) REFERENCES severity (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE INDEX "IX_idf_curve_event_type_id" ON idf_curve (event_type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE INDEX "IX_risk_matrix_event_type_id" ON risk_matrix (event_type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE INDEX "IX_severity_criterion_event_type_id" ON severity_criterion (event_type_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE INDEX "IX_severity_criterion_severity_id" ON severity_criterion (severity_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE UNIQUE INDEX ix_users_email ON users ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    CREATE UNIQUE INDEX ix_users_username ON users ("Username");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027021555_InitialBaseline') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027021555_InitialBaseline', '9.0.20');
    END IF;
END $EF$;
COMMIT;

-- ALERT ORCHESTRATOR SERVICE DATABASE SCHEMA

\c alert_orchestrator;

-- Generated from the EF Core migrations (services/alert-orchestrator/src/AlertOrchestrator.Infrastructure/Persistence/Migrations):
--   dotnet ef migrations script --idempotent --project services/alert-orchestrator/src/AlertOrchestrator.Infrastructure \
--     --startup-project services/alert-orchestrator/src/AlertOrchestrator.Infrastructure
-- Databases created by the previous version of this script: run baselines/alert_orchestrator-InitialBaseline-align.sql.

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    CREATE TABLE alert_windows (
        id uuid NOT NULL,
        region character varying(100) NOT NULL,
        risk_type character varying(50) NOT NULL,
        tenant_id character varying(50),
        opened_at timestamp with time zone NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        status character varying(20) NOT NULL,
        threshold double precision NOT NULL,
        triggered_at timestamp with time zone,
        final_risk_score double precision,
        closed_at timestamp with time zone,
        closed_by character varying(100),
        close_reason character varying(500),
        current_escalation_level character varying(20) NOT NULL DEFAULT 'Level1',
        confirmed_at timestamp with time zone,
        CONSTRAINT "PK_alert_windows" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    CREATE TABLE alert_signals (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        source character varying(20) NOT NULL,
        timestamp timestamp with time zone NOT NULL,
        event_id uuid NOT NULL,
        risk_score double precision NOT NULL,
        risk_type character varying(50) NOT NULL,
        metadata jsonb NOT NULL,
        alert_window_id uuid NOT NULL,
        CONSTRAINT "PK_alert_signals" PRIMARY KEY (id),
        CONSTRAINT "FK_alert_signals_alert_windows_alert_window_id" FOREIGN KEY (alert_window_id) REFERENCES alert_windows (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    CREATE UNIQUE INDEX alert_signals_event_id_key ON alert_signals (event_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    CREATE INDEX "IX_alert_signals_alert_window_id" ON alert_signals (alert_window_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    CREATE INDEX idx_alert_windows_confirmed_at ON alert_windows (confirmed_at) WHERE confirmed_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    CREATE INDEX idx_alert_windows_expires_at ON alert_windows (expires_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    CREATE INDEX idx_alert_windows_region_risk_status ON alert_windows (region, risk_type, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    CREATE INDEX idx_alert_windows_status ON alert_windows (status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    CREATE INDEX idx_alert_windows_tenant_id ON alert_windows (tenant_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261025161922_InitialBaseline') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261025161922_InitialBaseline', '9.0.20');
    END IF;
END $EF$;
COMMIT;

-- CHANNELS SERVICE DATABASE SCHEMA

\c channels;

-- Generated from the EF Core migrations (services/channels/src/ChannelsService.Infrastructure/Persistence/Migrations):
--   dotnet ef migrations script --idempotent --project services/channels/src/ChannelsService.Infrastructure \
--     --startup-project services/channels/src/ChannelsService.Api

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625031710_InitialMigration') THEN
    CREATE TABLE delivery_attempts (
        "AttemptId" character varying(64) NOT NULL,
        "EventId" character varying(128) NOT NULL,
        "TenantId" character varying(64) NOT NULL,
        "Channel" integer NOT NULL,
        "Provider" character varying(128) NOT NULL,
        "Status" integer NOT NULL,
        "AttemptCount" integer NOT NULL,
        "ErrorMessage" character varying(1000),
        "Timestamp" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_delivery_attempts" PRIMARY KEY ("AttemptId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260625031710_InitialMigration') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260625031710_InitialMigration', '9.0.20');
    END IF;
END $EF$;
COMMIT;

-- TENANTS BILLING SERVICE DATABASE SCHEMA

\c tenants_billing;

-- Generated from the EF Core migrations (services/tenants-billing/src/TenantsBilling.Infrastructure/Persistence/Migrations):
--   dotnet ef migrations script --idempotent --project services/tenants-billing/src/TenantsBilling.Infrastructure \
--     --startup-project services/tenants-billing/src/TenantsBilling.Api

CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816214308_InitialCreate') THEN
    CREATE TABLE plans (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "MaxEventsPerMonth" integer NOT NULL,
        "MaxAlertsPerMonth" integer NOT NULL,
        "MaxApiRequestsPerMonth" integer NOT NULL,
        "MaxChannelsPerMonth" integer NOT NULL,
        "SoftLimitPercentage" integer NOT NULL,
        "HardLimitPercentage" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_plans" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816214308_InitialCreate') THEN
    CREATE TABLE tenants (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "PlanId" uuid NOT NULL,
        "Status" integer NOT NULL,
        "Region" character varying(100) NOT NULL,
        "TimeZone" character varying(80) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_tenants" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816214308_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816214308_InitialCreate', '9.0.20');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027030640_AddTenantUsage') THEN
    CREATE TABLE recorded_usage_events (
        "SourceEventId" character varying(200) NOT NULL,
        "TenantId" uuid NOT NULL,
        "Period" character varying(7) NOT NULL,
        "RecordedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_recorded_usage_events" PRIMARY KEY ("SourceEventId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027030640_AddTenantUsage') THEN
    CREATE TABLE tenant_usage (
        "TenantId" uuid NOT NULL,
        "Period" character varying(7) NOT NULL,
        "EventsConsumed" bigint NOT NULL,
        "AlertsTriggered" bigint NOT NULL,
        "ApiRequests" bigint NOT NULL,
        "ChannelUsage" bigint NOT NULL,
        "QuotaStatus" integer NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_tenant_usage" PRIMARY KEY ("TenantId", "Period")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027030640_AddTenantUsage') THEN
    CREATE INDEX "IX_recorded_usage_events_TenantId_Period" ON recorded_usage_events ("TenantId", "Period");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027030640_AddTenantUsage') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027030640_AddTenantUsage', '9.0.20');
    END IF;
END $EF$;
COMMIT;
