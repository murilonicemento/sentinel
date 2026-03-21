\c ingestion;

-- Seed inicial de tenants (IDs fixos para testes e ambientes de desenvolvimento)
INSERT INTO tenant (name, is_active, created_at)
SELECT * FROM (VALUES
    ('Tenant Alpha', true, now()),
    ('Tenant Beta', true, now()),
    ('Tenant Gamma', true, now())
) AS v(id, name, is_active, created_at)
WHERE NOT EXISTS (SELECT 1 FROM tenant WHERE id = v.id);
