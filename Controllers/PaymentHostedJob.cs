using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Caching.Distributed;
using RinhaBackend.Infra;
using StackExchange.Redis;

namespace RinhaBackend.Controllers;

public class PaymentHostedJob : BackgroundService
{
    private readonly Channel<PaymentProcessorRequest> _channel;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IDatabase _distributedCache;

    public PaymentHostedJob(
        Channel<PaymentProcessorRequest> channel,
        IServiceScopeFactory serviceScopeFactory,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _channel = channel;
        _serviceScopeFactory = serviceScopeFactory;
        _distributedCache = connectionMultiplexer.GetDatabase();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            var paymentProcessorRequest = await _channel.Reader.ReadAsync(cancellationToken);

            using var scope = _serviceScopeFactory.CreateScope();

            var bestClientService = scope.ServiceProvider.GetRequiredService<BestClientService>();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var bestClient = await bestClientService.GetBestClient(cancellationToken);
            var paymentClientDefault = httpClientFactory.CreateClient(bestClient);

            var fullUrl = bestClient == "default"
                ? $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR")}/payments"
                : $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR_FALLBACK")}/payments";

            try
            {
                await SendPaymentAsync(paymentProcessorRequest, paymentClientDefault, fullUrl, cancellationToken);
            }
            catch (Exception)
            {
                continue;
            }

            var payment = bestClient == "default"
                ? paymentProcessorRequest.ToPayment()
                : paymentProcessorRequest.ToPaymentFallback();

            await _distributedCache.StringSetAsync(
                $"Payment-{paymentProcessorRequest.CorrelationId}",
                JsonSerializer.Serialize(payment),
                TimeSpan.FromDays(1));
        }
    }

    private async Task SendPaymentAsync(
        PaymentProcessorRequest request,
        HttpClient client,
        string fullUrl,
        CancellationToken cancellationToken = default)
    {
        var requestUri = new Uri(fullUrl, UriKind.RelativeOrAbsolute);

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.PostAsync(requestUri, content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failed to send payment summary request: {response.ReasonPhrase}");
        }
    }
}