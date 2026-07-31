using Dealership.Infrastructure.Photos;
using Dealership.Infrastructure.Storage;
using Dealership.Api.Configuration;
using Dealership.Api.Endpoints;
using Dealership.Application;
using Dealership.Application.Intake;
using Dealership.Infrastructure;
using Dealership.Infrastructure.Configuration;
using Dealership.Infrastructure.Persistence;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Vehicles;
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
builder.Services.AddIntakeApplication();
builder.Services.AddPhotoApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSingleton(AppointmentScheduleConfiguration.Read(builder.Configuration));
builder.Services.AddS3Storage(builder.Configuration);
builder.Services.AddPhotos(builder.Configuration);
builder.Services.AddOptions<EmailDeliverySettings>()
    .Bind(builder.Configuration.GetSection(EmailDeliverySettings.SectionName))
    .ValidateOnStart();
builder.Services.AddSingleton<IMailTransport, SmtpMailTransport>();
builder.Services.AddSingleton<AuthMailLinks>();
builder.Services.AddSingleton<VerificationMailComposer>();
builder.Services.AddSingleton<PasswordResetMailComposer>();
builder.Services.AddScoped<IMailDeliverySource, VerificationMailSource>();
builder.Services.AddScoped<IMailDeliverySource, PasswordResetMailSource>();
builder.Services.AddScoped<IMailDeliverySource, StaffInvitationMailSource>();
builder.Services.AddScoped<IMailDeliverySource, AppointmentMailSource>();
builder.Services.AddScoped<IMailDeliverySource, ReservationMailSource>();
builder.Services.AddScoped<IMailDeliverySource, Dealership.Infrastructure.TestDrives.TestDriveMailSource>();
// Delivery shares the existing durable mail worker and claim/retry bookkeeping.
builder.Services.AddSingleton<Dealership.Infrastructure.Sales.SaleDeliveryMailComposer>();
builder.Services.AddScoped<IMailDeliverySource, Dealership.Infrastructure.Sales.SaleDeliveryMailSource>();
builder.Services.AddHostedService<MailDeliveryWorker>();
builder.Services.AddHostedService<ReservationExpiryWorker>();
builder.Services.AddSingleton<StaffExportWorker>();
builder.Services.AddSingleton<IStaffExportWorker>(provider => provider.GetRequiredService<StaffExportWorker>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<StaffExportWorker>());
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddApiAuthentication();

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

// Prepare missing-account verification before requests so cold initialization cannot become a login signal.
app.Services.GetRequiredService<MissingAccountPasswordHash>();

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
// Rate limit before CORS so preflight requests consume the API budget.
app.UseRateLimiter();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseUnverifiedStaffGate();

// Orchestrator probes share one source IP and must never be throttled into a container restart.
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow
})).DisableRateLimiting();

app.MapGet("/health/ready", async (DealershipDbContext db, CancellationToken cancellationToken) =>
    await db.Database.CanConnectAsync(cancellationToken)
        ? Results.Ok(new { status = "ready" })
        : Results.StatusCode(StatusCodes.Status503ServiceUnavailable)).DisableRateLimiting();

app.MapTaxonomyValuationEndpoints();
app.MapListingCommandEndpoints();
app.MapReservationCommandEndpoints();
app.MapReservationQueryEndpoints();
app.MapTestDriveCommandEndpoints();
app.MapTestDriveQueryEndpoints();
app.MapStaffTestDriveQueryEndpoints();
app.MapPublicListingEndpoints();
app.MapPromoEndpoints();
app.MapSettingsEndpoints();
app.MapPromoAdministrationEndpoints();
app.MapWarrantyAdministrationEndpoints();
app.MapSaleCommandEndpoints();
app.MapSaleQueryEndpoints();
app.MapListingQueryEndpoints();
app.MapListingPhotoEndpoints();
app.MapRegistrationEndpoints();
app.MapAuthenticationEndpoints();
app.MapAccountPictureEndpoints();
app.MapAccountUsernameEndpoints();
app.MapStaffInvitationEndpoints();
app.MapStaffAccountDirectoryEndpoints();
app.MapPerformanceQueryEndpoints();
app.MapCustomerAccountDirectoryEndpoints();
app.MapAccountSuspensionEndpoints();
app.MapAccountEditEndpoints();
app.MapStaffAccountAdministrationEndpoints();
app.MapCustomerSupportEndpoints();
app.MapPersonEndpoints();
app.MapPasswordRecoveryEndpoints();
app.MapBranchDiscoveryEndpoints();
app.MapIntakeDiscoveryEndpoints();
app.MapIntakePhotoEndpoints();
app.MapIntakeBookingEndpoints();
app.MapStaffIntakeEndpoints();
app.MapWorkCalendarQueryEndpoints();
app.MapInspectionDraftEndpoints();
app.MapInspectionPhotoEndpoints();
app.MapInspectionRepairEndpoints();
app.MapInspectionQueueEndpoints();

app.Run();
