using RinhaBackend.Infra;

namespace RinhaBackend;

public sealed record PaymenteRequest
{
    public required Guid CorrelationId { get; init; }

    public required decimal Amount { get; init; }

    public static implicit operator PaymentProcessorRequest(PaymenteRequest request)
    {
        return new()
        {
            CorrelationId = request.CorrelationId,
            Amount = request.Amount,
            RequestedAt = DateTime.UtcNow
        };
    }
}

public sealed record PaymenteSummaryRequest
{
    public required DateTime From { get; init; }
    
    public required DateTime To { get; init; }
}

public record struct PaymenteSummaryResponse
{
    public required PaymentResponse Default { get; set; }
    
    public required PaymentResponse Fallback { get; set; }
}

public record struct PaymentResponse
{
    public required int TotalRequests { get; init; }
    
    public required decimal TotalAmount { get; init; }
}