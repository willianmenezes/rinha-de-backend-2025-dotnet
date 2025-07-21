using System.Text.Json.Serialization;

namespace RinhaBackend.Models;

public record struct PaymentProcessorHealthCheckResponse
{
    [JsonPropertyName("failing")]
    public bool Failing { get; set; }

    [JsonPropertyName("minResponseTime")]
    public int MinResponseTime { get; set; }
}