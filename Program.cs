using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using RinhaBackend.Controllers;
using RinhaBackend.Data;
using RinhaBackend.Infra;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddHttpClient("default",
    client =>
    {
        client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR")!);
        client.Timeout = TimeSpan.FromMilliseconds(100);
    });
builder.Services.AddHttpClient("fallback",
    client =>
    {
        client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR_FALLBACK")!);
        client.Timeout = TimeSpan.FromMilliseconds(100);
    });
builder.Services.AddDbContext<RinhaDb>(options =>
{
    options.UseNpgsql(Environment.GetEnvironmentVariable("DB_CONNECTION"));
});

builder.Services.AddScoped<PaymentClient>();
builder.Services.AddScoped<BestClientService>();
builder.Services.AddHostedService<PaymentHostedJob>();
builder.Services.AddSingleton<Channel<PaymentProcessorRequest>>(_ =>
    Channel.CreateUnbounded<PaymentProcessorRequest>(new UnboundedChannelOptions()
    {
        SingleReader = true,
        AllowSynchronousContinuations = false
    }));

var app = builder.Build();

app.MapControllers();
app.Run();