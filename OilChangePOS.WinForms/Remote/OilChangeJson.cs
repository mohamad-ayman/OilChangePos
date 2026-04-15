using System.Text.Json;
using System.Text.Json.Serialization;

namespace OilChangePOS.WinForms.Remote;

internal static class OilChangeJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
