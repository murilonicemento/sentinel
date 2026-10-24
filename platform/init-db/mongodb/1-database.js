db = db.getSiblingDB("IngestionReadDatabase");

db.createUser({
    user: "app_user",
    pwd: "app_password",
    roles: [
        { role: "readWrite", db: "IngestionReadDatabase" }
    ]
});

db = db.getSiblingDB("RiskEvaluationDatabase");

db.createUser({
    user: "app_user",
    pwd: "app_password",
    roles: [
        { role: "readWrite", db: "RiskEvaluationDatabase" }
    ]
});


// Audit & Compliance: papel com privilégio mínimo. Registros de auditoria são append-only
// (sem update/remove em auditRecords); exportações e arquivos temporários podem ser atualizados/removidos.
var auditDb = db.getSiblingDB("AuditComplianceDatabase");
var auditPassword = process.env.AUDIT_COMPLIANCE_DB_PASSWORD;

auditDb.createRole({
    role: "auditComplianceApp",
    privileges: [
        {
            resource: { db: "AuditComplianceDatabase", collection: "auditRecords" },
            actions: ["find", "insert", "createIndex", "listIndexes"]
        },
        {
            resource: { db: "AuditComplianceDatabase", collection: "auditExports" },
            actions: ["find", "insert", "update", "createIndex", "listIndexes"]
        },
        {
            resource: { db: "AuditComplianceDatabase", collection: "auditExportFiles.files" },
            actions: ["find", "insert", "remove", "createIndex", "listIndexes"]
        },
        {
            resource: { db: "AuditComplianceDatabase", collection: "auditExportFiles.chunks" },
            actions: ["find", "insert", "remove", "createIndex", "listIndexes"]
        }
    ],
    roles: []
});

if (auditPassword) {
    auditDb.createUser({
        user: "audit_compliance_app",
        pwd: auditPassword,
        roles: [{ role: "auditComplianceApp", db: "AuditComplianceDatabase" }]
    });
} else {
    print("AUDIT_COMPLIANCE_DB_PASSWORD not set; audit_compliance_app user was not created.");
}
