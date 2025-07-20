using System.Globalization;
using System.Text.Json;

namespace RinhaBackend.Infra;

public sealed class PaymentClient
{
    public async Task PurgePaymentAsync(
        HttpClient client,
        CancellationToken cancellationToken = default)
    {
        client.DefaultRequestHeaders.Add("X-Rinha-Token", "123");
        var requestUri = new Uri("admin/purge-payments", UriKind.RelativeOrAbsolute);
        
        var response = await client.PostAsync(requestUri, null, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to send purge payment: {response.ReasonPhrase}");
        }
    }
    
    public async Task<PaymenProcessorSummaryResponse> GetPaymentSummaryAsync(
        PaymenteSummaryRequest request,
        HttpClient client,
        string fullUrl,
        CancellationToken cancellationToken = default)
    {
        client.DefaultRequestHeaders.Add("X-Rinha-Token", "123");
        var requestUri = new Uri($"{fullUrl}?from={request.From.ToString(CultureInfo.InvariantCulture)}&to={request.To.ToString(CultureInfo.InvariantCulture)}", UriKind.RelativeOrAbsolute);
        var response = await client.GetAsync(requestUri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to get payment summary: {response.ReasonPhrase}");
        }
        
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<PaymenProcessorSummaryResponse>(content);
    }
}