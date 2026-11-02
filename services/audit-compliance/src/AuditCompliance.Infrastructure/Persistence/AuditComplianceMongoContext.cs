using AuditCompliance.Infrastructure.Persistence.Documents;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;

namespace AuditCompliance.Infrastructure.Persistence;

public sealed class AuditComplianceMongoContext
{
    public const string AuditRecordsCollection = "auditRecords";
    public const string AuditExportsCollection = "auditExports";
    public const string ExportFilesBucket = "auditExportFiles";

    public AuditComplianceMongoContext(IMongoClient client, string databaseName)
    {
        // Confirmação em maioria com journaling: a mensagem só é confirmada no Kafka após gravação durável.
        Database = client.GetDatabase(databaseName)
            .WithWriteConcern(WriteConcern.WMajority.With(journal: true));

        AuditRecords = Database.GetCollection<AuditRecordDocument>(AuditRecordsCollection);
        AuditExports = Database.GetCollection<AuditExportDocument>(AuditExportsCollection);
        ExportFiles = new GridFSBucket(Database, new GridFSBucketOptions { BucketName = ExportFilesBucket });
    }

    public IMongoDatabase Database { get; }
    public IMongoCollection<AuditRecordDocument> AuditRecords { get; }
    public IMongoCollection<AuditExportDocument> AuditExports { get; }
    public IGridFSBucket ExportFiles { get; }
}
