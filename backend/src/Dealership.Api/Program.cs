using Dealership.Api.Configuration;
using Dealership.Api.Endpoints;
using Dealership.Application;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Serilog;

var environmentConfiguration = new ConfigurationBuilder()
    .AddEnvironmentVariables("ASPNETCORE_")
    .AddEnvironmentVariables("DOTNET_")
    .AddCommandLine(args)
    .Build();
var hostEnvironment = HostConfiguration.ResolveEnvironment(environmentConfiguration);

if (string.Equals(hostEnvironment, Environments.Development, StringComparison.OrdinalIgnoreCase))
{
    DotEnvLoader.LoadForDevelopment(Directory.GetCurrentDirectory());
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, EnvironmentName = hostEnvironment });

// PaaS hosts assign the listening port through PORT.
var hostPort = Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrEmpty(hostPort) && string.IsNullOrEmpty(builder.Configuration["ASPNETCORE_URLS"]))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{hostPort}");
}

HostConfiguration.ValidateAllowedHosts(builder.Configuration["AllowedHosts"]);

var frontendOrigin = builder.Configuration["APP_BASE_URL"];

if (string.IsNullOrWhiteSpace(frontendOrigin))
{
    throw new InvalidOperationException("APP_BASE_URL is required in every environment.");
}

builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(frontendOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    HostConfiguration.ConfigureForwardedHeaders(options, builder.Configuration);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseSerilogRequestLogging();
app.UseCors("AllowFrontend");

app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
}));

app.MapGet("/health/ready", async (DealershipDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

app.MapVehicleEndpoints();

app.Run();
