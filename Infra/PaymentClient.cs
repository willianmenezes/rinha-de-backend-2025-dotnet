using System.Text.Json;
using RinhaBackend.Data;

namespace RinhaBackend.Infra;

public sealed class PaymentClient
{
    private readonly HttpClient _httpClient;

    public PaymentClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task SendPaymentAsync(
        PaymentProcessorRequest request,
        string address,
        CancellationToken cancellationToken = default)
    {
        _httpClient.BaseAddress = new(address);
        var requestUri = new Uri("payments", UriKind.RelativeOrAbsolute);
        
        var content = new StringContent(
            JsonSerializer.Serialize(request), 
            System.Text.Encoding.UTF8,
            "application/json");
        
        var response = await _httpClient.PostAsync(requestUri, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to send payment summary request: {response.ReasonPhrase}");
        }
    }
    
    public async Task<PaymentProcessorHealthCheckResponse> GetHealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var requestUri = new Uri("payments/service-health", UriKind.RelativeOrAbsolute);
        
        var response = await _httpClient.GetAsync(requestUri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to get health check: {response.ReasonPhrase}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        
        return JsonSerializer.Deserialize<PaymentProcessorHealthCheckResponse>(content) 
               ?? throw new InvalidOperationException("Failed to deserialize health check response.");
    }
}

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

public sealed record PaymentProcessorHealthCheckResponse
{
    public required bool Failing { get; init; }
    
    public required int MinResponseTime { get; init; }
}