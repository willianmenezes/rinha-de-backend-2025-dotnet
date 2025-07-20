namespace RinhaBackend.Data;

public sealed class HealthCheck
{
    public Guid Id { get; set; }
    public string? BestClient { get; set; }
    public DateTime RequestedAt { get; set; }
}