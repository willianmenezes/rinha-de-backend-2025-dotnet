using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using RinhaBackend.Models;
using StackExchange.Redis;

namespace RinhaBackend.Controllers;

[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly Channel<PaymentProcessorRequest> _channel;
    private readonly IDatabase _databaseRedis;
    private IEnumerable<RedisKey> _chavesRedis;

    public PaymentsController(
        Channel<PaymentProcessorRequest> channel,
        IConnectionMultiplexer connectionMultiplexer)
    {
        _channel = channel;
        _databaseRedis = connectionMultiplexer.GetDatabase();
        _chavesRedis = connectionMultiplexer.GetServer(connectionMultiplexer.GetEndPoints()[0]).Keys();
    }

    [HttpGet("payments-summary")]
    public async Task<IActionResult> GetSummary([FromQuery] PaymenteSummaryRequest request)
    {
        var allItens = _chavesRedis.Where(x => x.ToString().StartsWith("Payment"));

        var paymentJson = await _databaseRedis.StringGetAsync(allItens.ToArray());

        var payments = paymentJson
            .Select(item => JsonSerializer.Deserialize<Payment>(item!));

        var paymentsFiltrados = payments
            .Where(p =>
                p is not null &&
                p.RequestedAt >= request.From && 
                p.RequestedAt <= request.To)
            .ToList();

        var totalRequestsDefault = paymentsFiltrados.Count(x => x!.Fallback is false);
        var totalAmountDefault = paymentsFiltrados.Where(x => x!.Fallback is false).Sum(p => p!.Amount);
        var totalRequestsFallback = paymentsFiltrados.Count(x => x!.Fallback);
        var totalAmountFallback = paymentsFiltrados.Where(x => x!.Fallback).Sum(p => p!.Amount);

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
    //
    // [HttpGet("admin/payments-summary")]
    // public async Task<IActionResult> GetAdminSummary(
    //     [FromQuery] PaymenteSummaryRequest request,
    //     CancellationToken cancellationToken)
    // {
    //     // var bestClient = await _bestClientService.GetBestClient(cancellationToken);
    //     var client = _httpClientFactory.CreateClient("default");
    //     var fullUrl = $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR")}/admin/payments-summary";
    //     var response = await _paymentClient.GetPaymentSummaryAsync(request, client, fullUrl, cancellationToken);
    //     return Ok(response);
    // }

    [HttpPost("payments")]
    public async Task<IActionResult> CreatePayment(
        [FromBody] PaymenteRequest request,
        CancellationToken cancellationToken)
    {
        PaymentProcessorRequest paymentProcessorRequest = request;
        await _channel.Writer.WriteAsync(paymentProcessorRequest, cancellationToken);
        return Created();
    }

    // [HttpPost("purge-payments")]
    // public async Task<IActionResult> PurgePayments(CancellationToken cancellationToken)
    // {
    //     var client = _httpClientFactory.CreateClient("default");
    //     var clientFallback = _httpClientFactory.CreateClient("fallback");
    //     await _paymentClient.PurgePaymentAsync(client, cancellationToken);
    //     await _paymentClient.PurgePaymentAsync(clientFallback, cancellationToken);
    //     foreach (var chave in _chavesRedis)
    //     {
    //         await _databaseRedis.KeyDeleteAsync(chave);
    //     }
    //
    //     return Created();
    // }
}