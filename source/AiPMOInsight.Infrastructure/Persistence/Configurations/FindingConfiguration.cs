using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

        // Provenance (analysis-pipeline change). Enums persist as strings so the DB stays readable
        // and stable if enum ordinals shift.
        builder.Property(f => f.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(f => f.Confidence)
            .HasColumnName("confidence")
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(f => f.ProducingAgent)
            .HasColumnName("producing_agent")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(f => f.RunId)
            .HasColumnName("run_id")
            .IsRequired();

        builder.Property(f => f.PromptVersion)
            .HasColumnName("prompt_version")
            .HasMaxLength(200);

        // Structured health signal (health-scoring change). Nullable — only Analysis findings carry
        // them. Enums persist as strings so the DB stays readable and stable if ordinals shift,
        // matching Kind/Confidence above.
        builder.Property(f => f.Area)
            .HasColumnName("area")
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(f => f.Severity)
            .HasColumnName("severity")
            .HasConversion<string>()
            .HasMaxLength(10);

        // Optional structured metric (add-finding-metric, #46). All nullable + additive. The typed
        // value+unit let consumers sum/sort without parsing the summary; the detail map carries structured
        // metadata (e.g. a recommendation's owner/deadline/action) as jsonb.
        builder.Property(f => f.MetricValue)
            .HasColumnName("metric_value");

        builder.Property(f => f.MetricUnit)
            .HasColumnName("metric_unit")
            .HasMaxLength(20);

        var detailComparer = new ValueComparer<IReadOnlyDictionary<string, string>?>(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => v == null ? 0 : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => v == null ? null : (IReadOnlyDictionary<string, string>)new Dictionary<string, string>((IDictionary<string, string>)v));

        builder.Property(f => f.MetricDetail)
            .HasColumnName("metric_detail")
            .HasColumnType("jsonb")
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => v == null ? null : (IReadOnlyDictionary<string, string>)JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null)!)
            .Metadata.SetValueComparer(detailComparer);

        builder.Property(f => f.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        // Findings are read by project key (Level-2 endpoint) — index it.
        builder.HasIndex(f => f.ProjectKey);

        // Re-analysis appends under a new run; grouping/filtering by run is a common access path.
        builder.HasIndex(f => f.RunId);

        builder.OwnsOne(f => f.Citation, citation =>
        {
            citation.Property(c => c.UploadId)
                .HasColumnName("citation_upload_id")
                .IsRequired();

            citation.Property(c => c.Locator)
                .HasColumnName("citation_locator")
                .HasMaxLength(500)
                .IsRequired();

            // Optional richer evidence (both nullable) — the extended citation shape.
            citation.Property(c => c.StructuredExcerpt)
                .HasColumnName("citation_structured_excerpt")
                .HasMaxLength(500);

            citation.Property(c => c.TextSnippet)
                .HasColumnName("citation_text_snippet")
                .HasMaxLength(2000);

            // Findings are read by cited upload id (upload-history endpoint) — index it, matching
            // the by-project-key and by-run access paths above.
            citation.HasIndex(c => c.UploadId);
        });

        builder.Navigation(f => f.Citation).IsRequired();
    }
}
