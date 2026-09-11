using System.Text.Json;
using System.Text.Json.Serialization;

namespace Licenses.Application.Audit;

public static class AuditMetadataJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(object metadata) => JsonSerializer.Serialize(metadata, Options);
}
