using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RinhaBackend.Data;

namespace RinhaBackend.Infra;

public class BestClientService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public BestClientService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<string> GetBestClient(RinhaDb rinhaDb, CancellationToken cancellationToken = default)
    {
        var healthCheck = await rinhaDb.HealthCheck
            .FirstOrDefaultAsync(cancellationToken);

        if (healthCheck == null)
        {
            healthCheck = new HealthCheck
            {
                Id = Guid.NewGuid(),
                BestClient = "default",
                RequestedAt = DateTime.UtcNow,
            };

            await rinhaDb.HealthCheck.AddAsync(healthCheck, cancellationToken);
            await rinhaDb.SaveChangesAsync(cancellationToken);
            return healthCheck.BestClient;
        }

        if (healthCheck.RequestedAt > DateTime.UtcNow.AddSeconds(-5))
        {
            return healthCheck.BestClient!;
        }

        var paymentClientDefault = _httpClientFactory.CreateClient("default");
        var paymentClientFallback = _httpClientFactory.CreateClient("fallback");

        var url = $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR")}/payments/service-health";
        var urlFallback = $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR_FALLBACK")}/payments/service-health";

        var responseDefault = GetHealthCheckAsync(paymentClientDefault, url, cancellationToken);
        var responseFallback = GetHealthCheckAsync(paymentClientFallback, urlFallback, cancellationToken);

        _ = await Task.WhenAll(responseDefault, responseFallback);

        healthCheck.RequestedAt = DateTime.UtcNow;

        if (responseDefault.Result?.Failing ?? false)
        {
            healthCheck.BestClient = "fallback";
        }

        if (responseFallback.Result?.Failing ?? false)
        {
            healthCheck.BestClient = "default";
        }

        healthCheck.BestClient =
            responseDefault.Result?.MinResponseTime <= responseFallback.Result?.MinResponseTime
                ? "default"
                : "fallback";

        rinhaDb.HealthCheck.Update(healthCheck);
        await rinhaDb.SaveChangesAsync(cancellationToken);
        return healthCheck.BestClient;
    }

    private async Task<PaymentProcessorHealthCheckResponse?> GetHealthCheckAsync(
        HttpClient httpClient,
        string fullUrl,
        CancellationToken cancellationToken = default)
    {
        var requestUri = new Uri(fullUrl, UriKind.RelativeOrAbsolute);
        try
        {
            var response = await httpClient.GetAsync(requestUri, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Failed to get health check: {response.ReasonPhrase}");
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<PaymentProcessorHealthCheckResponse>(content);
        }
        catch (Exception e)
        {
            return null;
        }
    }
}