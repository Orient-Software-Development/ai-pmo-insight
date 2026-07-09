using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using AiPMOInsight.Domain.Findings;

namespace AiPMOInsight.Infrastructure.Persistence.Configurations;

/// <summary>
/// Fluent mapping for <see cref="Finding"/>. The mandatory <see cref="Citation"/> is mapped as an
/// owned type (columns on the findings table), so a finding can never be stored without provenance.
/// snake_case columns.
/// </summary>
internal sealed class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.ToTable("findings");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.Id)
            .HasColumnName("id");

        builder.Property(f => f.ProjectKey)
            .HasColumnName("project_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(f => f.Summary)
            .HasColumnName("summary")
            .IsRequired();

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Findings are read by project key (Level-2 endpoint) — index it.
        builder.HasIndex(f => f.ProjectKey);

        builder.OwnsOne(f => f.Citation, citation =>
        {
            citation.Property(c => c.UploadId)
                .HasColumnName("citation_upload_id")
                .IsRequired();

            citation.Property(c => c.Locator)
                .HasColumnName("citation_locator")
                .HasMaxLength(500)
                .IsRequired();
        });

        builder.Navigation(f => f.Citation).IsRequired();
    }
}
