using System.Text.Json.Nodes;
using AwesomeAssertions;
using AiPMOInsight.Application.Features.Analysis.Agents;
using AiPMOInsight.Infrastructure.Analysis.Llm;
using Xunit;

namespace AiPMOInsight.Api.Tests;

/// <summary>
/// The reflection-based JSON Schema generator that turns a <c>TOutput</c> contract into a schema
/// constrained to the Anthropic structured-output subset (objects with
/// <c>additionalProperties:false</c>, arrays, primitives; all declared properties required).
/// Records are handled generically; <see cref="ReviewResult"/> uses a per-type override because its
/// dynamic-key dictionary cannot be expressed in the subset (design §Decision 1).
/// </summary>
public class JsonSchemaGeneratorTests
{
    private static JsonObject Prop(JsonObject schema, string name) =>
        schema["properties"]!.AsObject()[name]!.AsObject();

    private static IReadOnlyList<string> Required(JsonObject schema) =>
        schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();

    [Fact]
    public void Record_becomes_object_with_additionalProperties_false_and_all_required()
    {
        var schema = JsonSchemaGenerator.For<Critique>();

        schema["type"]!.GetValue<string>().Should().Be("object");
        schema["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
        Required(schema).Should().BeEquivalentTo("Target", "Concern", "Severity", "Suggestion");
        Prop(schema, "Target")["type"]!.GetValue<string>().Should().Be("string");
    }

    [Fact]
    public void List_of_records_becomes_array_of_objects()
    {
        var schema = JsonSchemaGenerator.For<ChallengeResult>();

        var critiques = Prop(schema, "Critiques");
        critiques["type"]!.GetValue<string>().Should().Be("array");

        var item = critiques["items"]!.AsObject();
        item["type"]!.GetValue<string>().Should().Be("object");
        item["additionalProperties"]!.GetValue<bool>().Should().BeFalse();
        item["properties"]!.AsObject().Should().ContainKey("Concern");
    }

    [Fact]
    public void List_of_primitive_records_maps_MinuteRiskExtraction()
    {
        var schema = JsonSchemaGenerator.For<MinuteRiskExtraction>();

        var risks = Prop(schema, "Risks");
        risks["type"]!.GetValue<string>().Should().Be("array");
        risks["items"]!.AsObject()["properties"]!.AsObject()
            .Should().ContainKeys("Title", "Kind", "Severity", "Rationale");
    }

    [Fact]
    public void Nested_record_property_becomes_nested_object()
    {
        var schema = JsonSchemaGenerator.For<NarrativeResult>();

        Prop(schema, "Status")["type"]!.GetValue<string>().Should().Be("string");
        var rec = Prop(schema, "Recommendation");
        rec["type"]!.GetValue<string>().Should().Be("object");
        rec["properties"]!.AsObject().Should().ContainKeys("Owner", "Deadline", "Action", "Rationale");
    }

    [Fact]
    public void ReviewResult_uses_fixed_audience_key_override()
    {
        var schema = JsonSchemaGenerator.For<ReviewResult>();

        var byAudience = Prop(schema, "QuestionsByAudience");
        byAudience["type"]!.GetValue<string>().Should().Be("object");
        byAudience["additionalProperties"]!.GetValue<bool>().Should().BeFalse();

        var audiences = byAudience["properties"]!.AsObject();
        audiences.Should().NotBeEmpty();
        // Every audience maps to an array of strings.
        foreach (var (_, node) in audiences)
        {
            node!.AsObject()["type"]!.GetValue<string>().Should().Be("array");
            node.AsObject()["items"]!.AsObject()["type"]!.GetValue<string>().Should().Be("string");
        }
    }

    [Fact]
    public void Unsupported_dynamic_dictionary_without_override_throws()
    {
        var act = () => JsonSchemaGenerator.For<UnmappedDictionaryShape>();

        act.Should().Throw<NotSupportedException>();
    }

    // A dynamic-key dictionary with no per-type override — must fail loudly, not emit invalid schema.
    private sealed record UnmappedDictionaryShape(IReadOnlyDictionary<string, string> Values);
}
