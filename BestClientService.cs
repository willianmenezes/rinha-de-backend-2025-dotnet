using System.Net;
using System.Text.Json;
using RinhaBackend.Models;
using StackExchange.Redis;

namespace RinhaBackend;

public class BestClientService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDatabase _distributedCache;

    public BestClientService(
        IHttpClientFactory httpClientFactory,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _httpClientFactory = httpClientFactory;
        _distributedCache = connectionMultiplexer.GetDatabase();
    }

    public async Task<string> GetBestClient(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "BestClientHealthCheck";
        var healthCheckString = await _distributedCache.StringGetAsync(cacheKey);
        
        if (!string.IsNullOrEmpty(healthCheckString))
        {
            var check = JsonSerializer.Deserialize<HealthCheck>(healthCheckString!);
            if (check?.BestClient != null)
            {
                return check.BestClient;
            }
        }

        var paymentClientDefault = _httpClientFactory.CreateClient("default");
        var paymentClientFallback = _httpClientFactory.CreateClient("fallback");

        var url = $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR")}/payments/service-health";
        var urlFallback = $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR_FALLBACK")}/payments/service-health";

        var responseDefault = GetHealthCheckAsync(paymentClientDefault, url, cancellationToken);
        var responseFallback = GetHealthCheckAsync(paymentClientFallback, urlFallback, cancellationToken);

        _ = await Task.WhenAll(responseDefault, responseFallback);

        var healthCheck = new HealthCheck
        {
            BestClient = "default"
        };

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
        
        await _distributedCache.StringSetAsync(
            cacheKey,
            JsonSerializer.Serialize(healthCheck),
            TimeSpan.FromSeconds(5));

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