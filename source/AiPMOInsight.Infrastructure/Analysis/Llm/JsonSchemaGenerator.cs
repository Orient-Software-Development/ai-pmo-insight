using System.Reflection;
using System.Text.Json.Nodes;
using AiPMOInsight.Application.Features.Analysis.Agents;

namespace AiPMOInsight.Infrastructure.Analysis.Llm;

/// <summary>
/// Turns an LLM output contract (<c>TOutput</c>) into a JSON Schema constrained to the Anthropic
/// structured-output subset: objects carry <c>additionalProperties:false</c> and list every declared
/// property in <c>required</c>; supported leaves are <c>string</c>/<c>integer</c>/<c>number</c>/
/// <c>boolean</c> and arrays thereof (design §Decision 1).
/// <para>
/// Records are generated generically by reflection. A dynamic-key dictionary cannot be expressed in
/// the subset (<c>additionalProperties</c> must be <c>false</c>), so such a type requires a per-type
/// override; <see cref="ReviewResult"/> supplies one keyed by the known audiences. A type the walker
/// cannot express and that has no override throws <see cref="NotSupportedException"/> — a new output
/// contract fails loudly at first call rather than emitting an invalid schema.
/// </para>
/// </summary>
internal static class JsonSchemaGenerator
{
    /// <summary>Known Review audiences — the fixed keys of <see cref="ReviewResult.QuestionsByAudience"/>.</summary>
    private static readonly string[] ReviewAudiences = ["Executive", "Sponsor", "Data Lead", "Peer PM"];

    private static readonly IReadOnlyDictionary<Type, Func<JsonObject>> Overrides =
        new Dictionary<Type, Func<JsonObject>> { [typeof(ReviewResult)] = ReviewResultSchema };

    public static JsonObject For<T>() => For(typeof(T));

    public static JsonObject For(Type type) =>
        Overrides.TryGetValue(type, out var build) ? build() : SchemaFor(type);

    private static JsonObject SchemaFor(Type type)
    {
        if (type == typeof(string)) return Primitive("string");
        if (type == typeof(bool)) return Primitive("boolean");
        if (IsInteger(type)) return Primitive("integer");
        if (IsNumber(type)) return Primitive("number");

        if (IsDictionary(type))
        {
            throw new NotSupportedException(
                $"Cannot generate a structured-output schema for dynamic-key dictionary type " +
                $"'{type.Name}': the Anthropic subset forbids 'additionalProperties' other than false. " +
                "Add a per-type override to JsonSchemaGenerator.");
        }

        var element = GetEnumerableElement(type);
        if (element is not null)
        {
            return new JsonObject { ["type"] = "array", ["items"] = SchemaFor(element) };
        }

        return ObjectSchema(type);
    }

    private static JsonObject ObjectSchema(Type type)
    {
        var properties = new JsonObject();
        var required = new JsonArray();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            properties[property.Name] = SchemaFor(property.PropertyType);
            required.Add(property.Name);
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject ReviewResultSchema()
    {
        var audiences = new JsonObject();
        var required = new JsonArray();
        foreach (var audience in ReviewAudiences)
        {
            audiences[audience] = new JsonObject { ["type"] = "array", ["items"] = Primitive("string") };
            required.Add(audience);
        }

        var byAudience = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = audiences,
            ["required"] = required,
            ["additionalProperties"] = false,
        };

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject { ["QuestionsByAudience"] = byAudience },
            ["required"] = new JsonArray("QuestionsByAudience"),
            ["additionalProperties"] = false,
        };
    }

    private static JsonObject Primitive(string type) => new() { ["type"] = type };

    private static bool IsInteger(Type t) =>
        t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte);

    private static bool IsNumber(Type t) =>
        t == typeof(double) || t == typeof(float) || t == typeof(decimal);

    private static bool IsDictionary(Type t) =>
        t.GetInterfaces().Prepend(t).Any(i => i.IsGenericType &&
            (i.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>) ||
             i.GetGenericTypeDefinition() == typeof(IDictionary<,>)));

    private static Type? GetEnumerableElement(Type t)
    {
        if (t == typeof(string)) return null;
        if (t.IsArray) return t.GetElementType();
        var enumerable = t.GetInterfaces().Prepend(t)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }
}
