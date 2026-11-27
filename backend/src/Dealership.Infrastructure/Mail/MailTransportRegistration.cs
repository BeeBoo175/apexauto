using Dealership.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dealership.Infrastructure.Mail;

public static class MailTransportRegistration
{
    // Registers outbound mail; Email:Mode alone chooses between SMTP and the Apps Script web app.
    public static IServiceCollection AddMailTransport(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailDeliverySettings>()
            .Bind(configuration.GetSection(EmailDeliverySettings.SectionName))
            .ValidateOnStart();
        // Request logging stays off so the private script address is not written to logs.
        services.AddHttpClient(AppsScriptMailTransport.ClientName).RemoveAllLoggers();
        services.AddSingleton<SmtpMailTransport>();
        services.AddSingleton<AppsScriptMailTransport>();
        services.AddSingleton<IMailTransport>(provider =>
        {
            var mode = provider.GetRequiredService<IOptions<EmailDeliverySettings>>().Value.Mode;

            if (mode == AppsScriptMailTransport.ModeName)
            {
                return provider.GetRequiredService<AppsScriptMailTransport>();
            }

            return provider.GetRequiredService<SmtpMailTransport>();
        });
        return services;
    }
}
