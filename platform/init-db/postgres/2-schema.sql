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

\c
risk_catalog;

CREATE TABLE event_type
(
    id          uuid PRIMARY KEY,
    code        varchar(50)  NOT NULL,
    name        varchar(100) NOT NULL,
    description varchar(255) NOT NULL,
    is_active   boolean      NOT NULL default true
);

CREATE TABLE severity
(
    id          uuid PRIMARY KEY,
    level       integer NOT NULL,
    description varchar(255)
);

CREATE TABLE severity_criterion
(
    id            uuid PRIMARY KEY,
    event_type_id uuid    NOT NULL REFERENCES event_type (id),
    severity_id   uuid    NOT NULL REFERENCES severity (id),
    min_value     double precision,
    max_value     double precision,
    unit          varchar(20),
    version       integer NOT NULL default 1
);

CREATE TABLE idf_curve
(
    id                  uuid PRIMARY KEY,
    event_type_id       uuid             NOT NULL REFERENCES event_type (id),
    duration_minutes    integer          NOT NULL,
    intensity           double precision NOT NULL,
    return_period_years integer          NOT NULL,
    version             integer          NOT NULL default 1
);

CREATE TABLE regional_parameter
(
    id                uuid PRIMARY KEY,
    adjustment_factor double precision NOT NULL,
    description       varchar(255)
);

CREATE TABLE risk_matrix
(
    id             uuid PRIMARY KEY,
    event_type_id  uuid    NOT NULL REFERENCES event_type (id),
    severity_level integer NOT NULL,
    risk_level     integer NOT NULL,
    version        integer NOT NULL default 1
);

-- ALERT ORCHESTRATOR SERVICE DATABASE SCHEMA

\c alert_orchestrator;

CREATE TABLE alert_windows
(
    id               uuid PRIMARY KEY,
    region           varchar(100)     NOT NULL,
    risk_type        varchar(50)      NOT NULL,
    tenant_id        varchar(50)      NULL,
    opened_at        timestamptz      NOT NULL,
    expires_at       timestamptz      NOT NULL,
    status           varchar(20)      NOT NULL,
    threshold        double precision NOT NULL,
    triggered_at     timestamptz      NULL,
    final_risk_score double precision NULL
);

CREATE TABLE alert_signals
(
    id            uuid PRIMARY KEY      DEFAULT gen_random_uuid(),
    alert_window_id uuid         NOT NULL REFERENCES alert_windows (id) ON DELETE CASCADE,
    source          varchar(20)  NOT NULL,
    timestamp       timestamptz  NOT NULL,
    event_id        uuid         NOT NULL,
    risk_score      double precision NOT NULL,
    risk_type       varchar(50)  NOT NULL,
    metadata        jsonb        NULL,
    UNIQUE (event_id)
);

CREATE INDEX idx_alert_windows_region_risk_status ON alert_windows (region, risk_type, status);
CREATE INDEX idx_alert_windows_status ON alert_windows (status);
CREATE INDEX idx_alert_windows_expires_at ON alert_windows (expires_at);
CREATE INDEX idx_alert_windows_tenant_id ON alert_windows (tenant_id);
CREATE INDEX idx_alert_signals_event_id ON alert_signals (event_id);
