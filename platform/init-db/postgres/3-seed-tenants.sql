\c ingestion;

-- Seed inicial de tenants (IDs fixos para testes e ambientes de desenvolvimento)
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

INSERT INTO tenant (id, name, is_active, created_at)
SELECT * FROM (VALUES
    (uuid_generate_v4(),'Tenant Alpha', true, now()),
    (uuid_generate_v4(),'Tenant Beta', true, now()),
    (uuid_generate_v4(),'Tenant Gamma', true, now())
) AS v(id, name, is_active, created_at)
WHERE NOT EXISTS (SELECT 1 FROM tenant WHERE id = v.id);
