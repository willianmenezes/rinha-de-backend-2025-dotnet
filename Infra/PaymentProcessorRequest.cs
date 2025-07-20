using RinhaBackend.Data;

namespace RinhaBackend.Infra;

public sealed record PaymentProcessorRequest
{
    public required Guid CorrelationId { get; init; }

    public required decimal Amount { get; init; }
    
    public required DateTime RequestedAt { get; init; }
    
    public Payment ToPayment()
    {
        return new Payment
        {
            CorrelationId = CorrelationId,
            Amount = Amount,
            RequestedAt = RequestedAt,
            Fallback = false 
        };
    }
    
    public Payment ToPaymentFallback()
    {
        return new Payment
        {
            CorrelationId = CorrelationId,
            Amount = Amount,
            RequestedAt = RequestedAt,
            Fallback = true 
        };
    }
}