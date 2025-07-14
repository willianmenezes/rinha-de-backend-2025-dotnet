using Microsoft.EntityFrameworkCore;
using RinhaBackend;
using RinhaBackend.Data;
using RinhaBackend.Infra;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddHttpClient<PaymentClient>();

builder.Services.AddDbContext<RinhaDb>(options =>
{
    options.UseNpgsql(Environment.GetEnvironmentVariable("DB_CONNECTION"));
});

builder.Services.AddScoped<RinhaDb>();

var app = builder.Build();

app.MapControllers();
app.Run();