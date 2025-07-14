namespace RinhaBackend.Data;

public sealed record Payment
{
    public Guid CorrelationId { get; set; }
    
    public decimal Amount { get; set; }
    
    public DateTime RequestedAt { get; set; }

    public bool Fallback { get; set; }
}