using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using RinhaBackend.Data;
using RinhaBackend.Infra;
using StackExchange.Redis;

namespace RinhaBackend.Controllers;

[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentClient _paymentClient;
    private readonly Channel<PaymentProcessorRequest> _channel;
    private readonly BestClientService _bestClientService;
    private readonly IDatabase _databaseRedis;
    private IEnumerable<RedisKey> _chavesRedis;

    public PaymentsController(
        IHttpClientFactory httpClientFactory,
        PaymentClient paymentClient,
        Channel<PaymentProcessorRequest> channel,
        BestClientService bestClientService,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _httpClientFactory = httpClientFactory;
        _paymentClient = paymentClient;
        _channel = channel;
        _bestClientService = bestClientService;
        _databaseRedis = connectionMultiplexer.GetDatabase();
        _chavesRedis = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints()[0]).Keys();
    }

    [HttpGet("payments-summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] PaymenteSummaryRequest request,
        CancellationToken cancellationToken)
    {
        var allItens = _chavesRedis.Where(x => x.ToString().StartsWith("Payment"));

        var paymentJson = await _databaseRedis.StringGetAsync(allItens.ToArray());

        var payments = paymentJson
            .Select(item => JsonSerializer.Deserialize<Payment>(item!))
            .ToList();

        var paymentsFiltrados = payments
            .Where(p => p.RequestedAt >= request.From && p.RequestedAt <= request.To);

        var totalRequestsDefault = paymentsFiltrados.Count(x => x.Fallback is false);
        var totalAmountDefault = paymentsFiltrados.Where(x => x.Fallback is false).Sum(p => p.Amount);
        var totalRequestsFallback = paymentsFiltrados.Count(x => x.Fallback);
        var totalAmountFallback = paymentsFiltrados.Where(x => x.Fallback).Sum(p => p.Amount);

        var response = new PaymenteSummaryResponse()
        {
            Default = new PaymentResponse()
            {
                TotalRequests = totalRequestsDefault,
                TotalAmount = totalAmountDefault
            },
            Fallback = new PaymentResponse()
            {
                TotalRequests = totalRequestsFallback,
                TotalAmount = totalAmountFallback
            }
        };
        return Ok(response);
    }

    [HttpGet("admin/payments-summary")]
    public async Task<IActionResult> GetAdminSummary(
        [FromQuery] PaymenteSummaryRequest request,
        CancellationToken cancellationToken)
    {
        var bestClient = await _bestClientService.GetBestClient(cancellationToken);
        var client = _httpClientFactory.CreateClient(bestClient);
        var fullUrl = $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR")}/admin/payments-summary";
        var response = await _paymentClient.GetPaymentSummaryAsync(request, client, fullUrl, cancellationToken);
        return Ok(response);
    }

    [HttpPost("payments")]
    public async Task<IActionResult> CreatePayment(
        [FromBody] PaymenteRequest request,
        CancellationToken cancellationToken)
    {
        PaymentProcessorRequest paymentProcessorRequest = request;
        await _channel.Writer.WriteAsync(paymentProcessorRequest, cancellationToken);
        return Created();
    }

    [HttpPost("purge-payments")]
    public async Task<IActionResult> PurgePayments(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("default");
        var clientFallback = _httpClientFactory.CreateClient("fallback");
        await _paymentClient.PurgePaymentAsync(client, cancellationToken);
        await _paymentClient.PurgePaymentAsync(clientFallback, cancellationToken);
        foreach (var chave in _chavesRedis)
        {
            await _databaseRedis.KeyDeleteAsync(chave);
        }

        return Created();
    }
}