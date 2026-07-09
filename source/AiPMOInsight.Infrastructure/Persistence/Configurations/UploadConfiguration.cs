using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiPMOInsight.Domain.Ingest;

namespace AiPMOInsight.Infrastructure.Persistence.Configurations;

/// <summary>Fluent mapping for <see cref="Upload"/> (ingest landing area). snake_case columns.</summary>
internal sealed class UploadConfiguration : IEntityTypeConfiguration<Upload>
{
    public void Configure(EntityTypeBuilder<Upload> builder)
    {
        builder.ToTable("uploads");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasColumnName("id");

        builder.Property(u => u.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(u => u.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(u => u.UploadedAt)
            .HasColumnName("uploaded_at")
            .IsRequired();
    }
}
