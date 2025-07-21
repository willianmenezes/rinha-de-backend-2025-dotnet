using System.Threading.Channels;
using RinhaBackend;
using RinhaBackend.Controllers;
using RinhaBackend.Models;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddHttpClient("default",
    client =>
    {
        client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR")!);
    });
builder.Services.AddHttpClient("fallback",
    client =>
    {
        client.BaseAddress = new Uri(Environment.GetEnvironmentVariable("PAYMENT_PROCESSOR_FALLBACK")!);
    });

builder.Services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(Environment.GetEnvironmentVariable("CACHE")!));
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