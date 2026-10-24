db = db.getSiblingDB("IngestionReadDatabase");

db.createCollection("climaticEvents");
db.createCollection("disasterEvents");

// Risk Evaluation database schema
bdr = db.getSiblingDB("RiskEvaluationDatabase");

bdr.createCollection("RiskEvaluations");

bdr.RiskEvaluations.createIndex({ Id: 1 }, { unique: true });
bdr.RiskEvaluations.createIndex({ Location: 1 });
bdr.RiskEvaluations.createIndex({ Timestamp: -1 });

db.climaticEvents.createIndex({EventId: 1}, {unique: true});
db.climaticEvents.createIndex({EventType: 1});
db.climaticEvents.createIndex({CollectedAt: -1});

db.disasterEvents.createIndex({EventId: 1}, {unique: true});
db.disasterEvents.createIndex({EventType: 1});
db.disasterEvents.createIndex({CollectedAt: -1});

db.climaticEvents.insertMany([
    {
        EventId: UUID(),
        EventType: "TemperatureAnomaly",
        Intensity: 12.5,
        Latitude: -23.5505,
        Longitude: -46.6333,
        CollectedAt: ISODate()
    },
    {
        EventId: UUID(),
        EventType: "HumidityAnomaly",
        Intensity: 40.2,
        Latitude: -22.9068,
        Longitude: -43.1729,
        CollectedAt: ISODate()
    }
]);

db.disasterEvents.insertMany([
    {
        EventId: UUID(),
        EventType: "Wildfire",
        Intensity: 12.5,
        Latitude: -23.5505,
        Longitude: -46.6333,
        CollectedAt: ISODate()
    },
    {
        EventId: UUID(),
        EventType: "Earthquake",
        Intensity: 40.2,
        Latitude: -22.9068,
        Longitude: -43.1729,
        CollectedAt: ISODate()
    }
]);

// Audit & Compliance database schema (manter em paridade com MongoIndexInitializer)
var adb = db.getSiblingDB("AuditComplianceDatabase");

adb.createCollection("auditRecords");
adb.createCollection("auditExports");
adb.createCollection("auditExportFiles.files");
adb.createCollection("auditExportFiles.chunks");

adb.auditRecords.createIndex({ SourceService: 1, EventId: 1 }, { unique: true, name: "ux_source_service_event_id" });
adb.auditRecords.createIndex({ TenantId: 1, OccurredAt: -1, _id: -1 }, { name: "ix_tenant_occurred_at" });
adb.auditRecords.createIndex({ TenantId: 1, EventType: 1, OccurredAt: -1 }, { name: "ix_tenant_event_type_occurred_at" });
adb.auditRecords.createIndex({ TenantId: 1, "Actor.Id": 1, OccurredAt: -1 }, { name: "ix_tenant_actor_occurred_at" });
adb.auditRecords.createIndex({ TenantId: 1, "Resource.Id": 1, OccurredAt: -1 }, { name: "ix_tenant_resource_occurred_at" });
adb.auditRecords.createIndex({ TenantId: 1, CorrelationId: 1 }, { name: "ix_tenant_correlation_id" });

adb.auditExports.createIndex({ Status: 1, RequestedAt: 1 }, { name: "ix_status_requested_at" });
adb.auditExports.createIndex({ Status: 1, ExpiresAt: 1 }, { name: "ix_status_expires_at" });

adb.getCollection("auditExportFiles.files").createIndex({ filename: 1, uploadDate: 1 });
adb.getCollection("auditExportFiles.chunks").createIndex({ files_id: 1, n: 1 }, { unique: true });
