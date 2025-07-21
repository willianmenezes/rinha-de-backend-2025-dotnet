namespace RinhaBackend;

public sealed record HealthCheck
{
    public string? BestClient { get; set; }
}