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

CREATE TABLE data_collection
(
    id             uuid PRIMARY KEY,
    data_source_id uuid        NOT NULL REFERENCES data_source (id),
    collected_at   timestamptz NOT NULL,
    payload        jsonb       NOT NULL,
    tenant_id      uuid NULL,
    created_at     timestamptz not null default now(),
    CONSTRAINT uq_datasource_collectedat UNIQUE (data_source_id, collected_at)
);

CREATE TABLE sensor_sample
(
    id                 uuid PRIMARY KEY,
    data_collection_id uuid             NOT NULL REFERENCES data_collection (id),
    sensor_value       double precision NOT NULL,
    unit               varchar(20)      NOT NULL,
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
