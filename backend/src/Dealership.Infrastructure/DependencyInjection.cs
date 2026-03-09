using Dealership.Application.Photos;
using Dealership.Infrastructure.Configuration;
using Dealership.Application.Accounts;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Accounts;
using Dealership.Application.Branches;
using Dealership.Infrastructure.Branches;
using Dealership.Application.Inspections;
using Dealership.Infrastructure.Inspections;
using Dealership.Infrastructure.Mail;
using Dealership.Infrastructure.Photos;

namespace Dealership.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__DefaultConnection is required in every environment.");
        }

        services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<MailDeliverySignal>();
        services.AddSingleton<PhotoCleanupSignal>();
        services.AddIdentityCore<AccountUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            // Email is the login identifier; a separate username alphabet must not narrow valid emails.
            options.User.AllowedUserNameCharacters = string.Empty;
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<DealershipDbContext>()
            .AddPasswordValidator<AccountPasswordValidator>();
        services.AddScoped<ICustomerRegistrationStore, CustomerRegistrationStore>();
        services.AddScoped<IAccountSessionStore, AccountSessionStore>();
        services.AddScoped<ICustomerAccountQuery, CustomerAccountQuery>();
        services.AddScoped<IStaffProvisioningStore, StaffProvisioningStore>();
        services.AddScoped<IBranchProvisioningStore, BranchProvisioningStore>();
        services.AddScoped<IEmailVerificationStore, EmailVerificationStore>();
        services.AddSingleton<EmailVerificationToken>();
        services.AddScoped<IPasswordRecoveryStore, PasswordRecoveryStore>();
        services.AddSingleton<PasswordResetToken>();
        services.AddScoped<IPublicBranchStore, PublicBranchStore>();
        services.AddScoped<IIntakePhotoStore, IntakePhotoStore>();
        services.AddScoped<IPhotoTransferLock, PhotoTransferLock>();
        services.AddScoped<IIntakeBookingStore, IntakeBookingStore>();
        services.AddScoped<IAppointmentHistoryStore, AppointmentHistoryStore>();
        services.AddScoped<IAppointmentCancellationStore, AppointmentCancellationStore>();
        services.AddScoped<IAppointmentCheckInStore, AppointmentCheckInStore>();
        services.AddScoped<IInspectionAssignmentStore, InspectionAssignmentStore>();
        services.AddScoped<IInspectionDraftSaveStore, InspectionDraftSaveStore>();
        services.AddScoped<IInspectionReportStore, InspectionReportStore>();
        services.AddScoped<IInspectionSignoffStore, InspectionSignoffStore>();
        services.AddScoped<IInspectionReopeningStore, InspectionReopeningStore>();
        services.AddScoped<IInspectionRepairRequestStore, InspectionRepairRequestStore>();
        services.AddScoped<IInspectionRepairApprovalStore, InspectionRepairApprovalStore>();
        services.AddScoped<IInspectionRepairCompletionStore, InspectionRepairCompletionStore>();
        services.AddScoped<IInspectionHistoryStore, InspectionHistoryStore>();
        services.AddScoped<IInspectionQueueStore, InspectionQueueStore>();
        services.AddScoped<IWorkshopQueueStore, WorkshopQueueStore>();
        services.AddScoped<IInspectionPhotoStore, InspectionPhotoStore>();
        services.AddScoped<IStaffWorklistStore, StaffWorklistStore>();
        services.AddScoped<IStaffExportWriter, StaffWorklistStore>();
        services.AddScoped<IStaffAppointmentDetailStore, StaffAppointmentDetailStore>();
        services.AddSingleton<MissingAccountPasswordHash>(provider =>
        {
            using var scope = provider.CreateScope();
            return new MissingAccountPasswordHash(scope.ServiceProvider.GetRequiredService<IPasswordHasher<AccountUser>>());
        });

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}
