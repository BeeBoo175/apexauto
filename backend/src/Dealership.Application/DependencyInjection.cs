using Dealership.Application.Vehicles;
using Dealership.Application.Accounts;
using Dealership.Application.Branches;
using Dealership.Application.Intake;
using Dealership.Application.Inspections;
using Dealership.Application.Photos;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ValuationService>();
        services.AddScoped<ListingService>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<StaffProvisioningService>();
        services.AddScoped<BranchProvisioningService>();
        services.AddScoped<BranchDiscoveryService>();
        services.AddScoped<EmailVerificationService>();
        services.AddScoped<PasswordRecoveryService>();
        services.AddScoped<AppointmentCheckInService>();
        services.AddScoped<AppointmentCancellationService>();
        services.AddScoped<InspectionAssignmentService>();
        services.AddScoped<InspectionDraftSaveService>();
        services.AddScoped<InspectionSignoffService>();
        services.AddScoped<InspectionReopeningService>();
        services.AddScoped<InspectionRepairRequestService>();
        services.AddScoped<InspectionRepairApprovalService>();
        services.AddScoped<InspectionRepairCompletionService>();

        return services;
    }

    public static IServiceCollection AddIntakeApplication(this IServiceCollection services)
    {
        services.AddScoped<IntakeDiscoveryService>();
        services.AddScoped<IntakeBookingService>();
        services.AddScoped<StaffCalendarService>();
        return services;
    }

    public static IServiceCollection AddPhotoApplication(this IServiceCollection services)
    {
        services.AddScoped<PhotoTransferService>();
        services.AddScoped<IntakePhotoService>();
        services.AddScoped<InspectionPhotoService>();
        return services;
    }
}
