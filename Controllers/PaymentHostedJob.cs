using System.Net;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using RinhaBackend.Data;
using RinhaBackend.Infra;

namespace RinhaBackend.Controllers;

public class PaymentHostedJob : BackgroundService
{
    private readonly Channel<PaymentProcessorRequest> _channel;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public PaymentHostedJob(
        Channel<PaymentProcessorRequest> channel,
        IServiceScopeFactory serviceScopeFactory)
    {
        _channel = channel;
        _serviceScopeFactory = serviceScopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (await _channel.Reader.WaitToReadAsync(cancellationToken))
        {
            var paymentProcessorRequest = await _channel.Reader.ReadAsync(cancellationToken);

            using var scope = _serviceScopeFactory.CreateScope();

            var rinhaDb = scope.ServiceProvider.GetRequiredService<RinhaDb>();
            var bestClientService = scope.ServiceProvider.GetRequiredService<BestClientService>();
            var httpClientFactory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
            var bestClient = await bestClientService.GetBestClient(rinhaDb!, cancellationToken);
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

            await rinhaDb.Payments.AddAsync(payment, cancellationToken);
            await rinhaDb.SaveChangesAsync(cancellationToken);
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