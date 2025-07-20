using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RinhaBackend.Data;
using RinhaBackend.Infra;

namespace RinhaBackend.Controllers;

[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly PaymentClient _paymentClient;
    private readonly Channel<PaymentProcessorRequest> _channel;
    private readonly RinhaDb _rinhaDb;
    private readonly BestClientService _bestClientService;

    public PaymentsController(
        IHttpClientFactory httpClientFactory,
        PaymentClient paymentClient,
        Channel<PaymentProcessorRequest> channel,
        RinhaDb rinhaDb, BestClientService bestClientService)
    {
        _httpClientFactory = httpClientFactory;
        _paymentClient = paymentClient;
        _channel = channel;
        _rinhaDb = rinhaDb;
        _bestClientService = bestClientService;
    }

    [HttpGet("payments-summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] PaymenteSummaryRequest request,
        CancellationToken cancellationToken)
    {
        var payments = await _rinhaDb.Payments
            .AsNoTracking()
            .Where(p => p.RequestedAt >= request.From && p.RequestedAt <= request.To)
            .ToListAsync(cancellationToken);

        var totalRequestsDefault = payments.Count(x => x.Fallback is false);
        var totalAmountDefault = payments.Where(x => x.Fallback is false).Sum(p => p.Amount);
        var totalRequestsFallback = payments.Count(x => x.Fallback);
        var totalAmountFallback = payments.Where(x => x.Fallback).Sum(p => p.Amount);

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
        var bestClient = await _bestClientService.GetBestClient(_rinhaDb, cancellationToken);
        var client = _httpClientFactory.CreateClient(bestClient);
        var fullUrl = bestClient == "default"
            ? $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR")}/admin/payments-summary"
            : $"{Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR_FALLBACK")}/admin/payments-summary";
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
        await _rinhaDb.Payments.ExecuteDeleteAsync(cancellationToken);
        await _rinhaDb.SaveChangesAsync(cancellationToken);
        return Created();
    }
}