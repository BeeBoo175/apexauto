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
        services.AddScoped<ReservationService>();
        services.AddScoped<ReservationDepositRequestService>();
        services.AddScoped<ReservationPaymentService>();
        services.AddScoped<ReservationRefundService>();
        services.AddScoped<ReservationCancellationService>();
        services.AddScoped<PurchaseQuoteService>();
        services.AddScoped<ListingEstimateService>();
        services.AddScoped<PurchaseConfigurationService>();
        services.AddScoped<PurchaseSettingsAdministrationService>();
        services.AddScoped<RetentionSettingsService>();
        services.AddScoped<PromoAdministrationService>();
        services.AddScoped<WarrantyAdministrationService>();
        services.AddScoped<Sales.SaleHandlingService>();
        services.AddScoped<Sales.SaleTermsService>();
        services.AddScoped<Sales.SaleSettlementService>();
        services.AddScoped<Sales.SaleHandoverService>();
        services.AddScoped<Sales.SaleDocumentCorrectionService>();
        services.AddScoped<Sales.SaleRetentionHoldService>();
        services.AddScoped<Sales.SaleDocumentService>();
        services.AddScoped<RegistrationService>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<PersonViewService>();
        services.AddScoped<AccountUsernameService>();
        services.AddScoped<CustomerClosureService>();
        services.AddScoped<AccountErasureService>();
        services.AddScoped<AccountSuspensionService>();
        services.AddScoped<AccountEditService>();
        services.AddScoped<StaffAccountAdministrationService>();
        services.AddScoped<CustomerSupportService>();
        services.AddScoped<StaffInvitationService>();
        services.AddScoped<StaffProvisioningService>();
        services.AddScoped<BranchProvisioningService>();
        services.AddScoped<BranchDiscoveryService>();
        services.AddScoped<EmailVerificationService>();
        services.AddScoped<PasswordRecoveryService>();
        services.AddScoped<AppointmentCheckInService>();
        services.AddScoped<AppointmentAssignmentService>();
        services.AddScoped<AppointmentCancellationService>();
        services.AddScoped<TestDrives.TestDriveCancellationService>();
        services.AddScoped<TestDrives.TestDriveAssignmentService>();
        services.AddScoped<TestDrives.TestDriveHandlingService>();
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
        services.AddScoped<TestDrives.TestDriveBookingService>();
        services.AddScoped<TestDrives.TestDriveDiscoveryService>();
        services.AddScoped<IntakeDiscoveryService>();
        services.AddScoped<IntakeBookingService>();
        return services;
    }

    public static IServiceCollection AddPhotoApplication(this IServiceCollection services)
    {
        services.AddScoped<PhotoTransferService>();
        services.AddScoped<IntakePhotoService>();
        services.AddScoped<InspectionPhotoService>();
        services.AddScoped<InspectionReportDocumentService>();
        services.AddScoped<ListingPhotoService>();
        services.AddScoped<AccountPictureService>();
        services.AddScoped<Sales.SaleVehiclePhotoService>();
        return services;
    }
}
