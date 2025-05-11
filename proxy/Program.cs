using Serilog;
using SIPWreck.SIP;

Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<RegistrarService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<RegistrarService>());
builder.Services.AddHostedService<ProxyService>();
builder.Services.AddOpenApi();

builder.Host.UseSerilog();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

await app.RunAsync();
