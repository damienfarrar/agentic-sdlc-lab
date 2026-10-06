using System.Text.Json.Serialization;

namespace Lab.PlatformApi.Commands;

[JsonConverter(typeof(JsonStringEnumConverter<DiagnosticsSection>))]
public enum DiagnosticsSection
{
    Environment,
    Service,
}
