using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RinhaBackend.Data;
using RinhaBackend.Infra;

namespace RinhaBackend.Controllers;

[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly PaymentClient _paymentClient;
    private readonly RinhaDb _rinhaDb;

    public PaymentsController(
        PaymentClient paymentClient,
        RinhaDb rinhaDb)
    {
        _paymentClient = paymentClient;
        _rinhaDb = rinhaDb;
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

    [HttpPost("payments")]
    public async Task<IActionResult> CreatePayment(
        [FromBody] PaymenteRequest request,
        CancellationToken cancellationToken)
    {
        PaymentProcessorRequest paymentProcessorRequest = request;
        Payment payment;
        try
        {
            var address = Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR")!;
            await _paymentClient.SendPaymentAsync(paymentProcessorRequest, address, cancellationToken);
            payment = paymentProcessorRequest.ToPayment();
        }
        catch (HttpRequestException)
        {
            var address = Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR_FALLBACK")!;
            await _paymentClient.SendPaymentAsync(paymentProcessorRequest, address, cancellationToken);
            payment = paymentProcessorRequest.ToPaymentFallback();
        }

        await _rinhaDb.Payments.AddAsync(payment, cancellationToken);
        await _rinhaDb.SaveChangesAsync(cancellationToken);
        return Created();
    }
}