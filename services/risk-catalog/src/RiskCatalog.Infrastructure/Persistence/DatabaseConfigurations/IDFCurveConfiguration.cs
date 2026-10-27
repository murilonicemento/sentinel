using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Infrastructure.Persistence.DatabaseConfigurations;

public class IDFCurveConfiguration : IEntityTypeConfiguration<IDFCurve>
{
    public void Configure(EntityTypeBuilder<IDFCurve> builder)
    {
        builder.ToTable("idf_curve");
        builder.HasKey(idf => idf.Id);
        builder.Property(et => et.Id)
            .IsRequired()
            .HasColumnType("uuid")
            .HasColumnName("id");
        builder.Property(idf => idf.EventTypeId)
            .IsRequired()
            .HasColumnType("uuid")
            .HasColumnName("event_type_id");
        builder.Property(idf => idf.DurationMinutes)
            .IsRequired()
            .HasColumnType("integer")
            .HasColumnName("duration_minutes");
        builder.Property(idf => idf.Intensity)
            .IsRequired()
            .HasColumnType("double precision")
            .HasColumnName("intensity");
        builder.Property(idf => idf.ReturnPeriodYears)
            .IsRequired()
            .HasColumnType("integer")
            .HasColumnName("return_period_years");
        builder.Property(idf => idf.Version)
            .IsRequired()
            .HasColumnType("integer")
            .HasColumnName("version");

        builder.HasOne(idf => idf.EventType).WithMany(rm => rm.IDFCurves).HasForeignKey(idf => idf.EventTypeId);
    }
}