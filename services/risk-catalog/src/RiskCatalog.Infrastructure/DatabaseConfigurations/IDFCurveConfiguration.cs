using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RiskCatalog.Domain.RiskModels;

namespace RiskCatalog.Infrastructure.DatabaseConfigurations;

public class IDFCurveConfiguration : IEntityTypeConfiguration<IDFCurve>
{
    public void Configure(EntityTypeBuilder<IDFCurve> builder)
    {
        builder.ToTable("idf_curve");
        builder.HasKey(idf => idf.Id);
        builder.Property(et => et.Id)
            .IsRequired()
            .HasColumnType("uniqueidentifier")
            .HasColumnName("id");
        builder.Property(idf => idf.EventTypeId)
            .IsRequired()
            .HasColumnType("uniqueidentifier")
            .HasColumnName("event_type_id");
        builder.Property(idf => idf.DurationMinutes)
            .IsRequired()
            .HasColumnType("int")
            .HasColumnName("duration_minutes");
        builder.Property(idf => idf.Intensity)
            .IsRequired()
            .HasColumnType("double")
            .HasColumnName("intensity");
        builder.Property(idf => idf.ReturnPeriodYears)
            .IsRequired()
            .HasColumnType("int")
            .HasColumnName("return_period_years");
        builder.Property(idf => idf.Version)
            .IsRequired()
            .HasColumnType("int")
            .HasColumnName("version");

        builder.HasOne(idf => idf.EventType).WithMany(rm => rm.IDFCurves).HasForeignKey(idf => idf.EventTypeId);
    }
}