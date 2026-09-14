using Dealership.Infrastructure.Retention;
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
using Dealership.Application.Vehicles;
using Dealership.Infrastructure.Vehicles;

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
        services.AddSingleton<ReservationExpirySignal>();
        services.AddSingleton<RetentionSignal>();
        services.AddOptions<RetentionOptions>().Bind(configuration.GetSection("Retention"))
            .Validate(settings => settings.BatchSize is >= 1 and <= 200,
                "Retention requires a batch size between 1 and 200.").ValidateOnStart();
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
        services.AddScoped<IListingStore, ListingStore>();
        services.AddScoped<IReservationStore, ReservationStore>();
        services.AddScoped<IReservationDepositRequestStore, ReservationDepositRequestStore>();
        services.AddSingleton<ReservationDepositToken>();
        services.AddScoped<IReservationPaymentStore, ReservationPaymentStore>();
        services.AddScoped<IReservationRefundStore, ReservationRefundStore>();
        // The one place the payment provider is chosen: another provider registers its own IPaymentGateway and
        // IPayoutGateway here instead, and nothing else in the application changes.
        services.AddBillplzPayments(configuration);
        services.AddScoped<IReservationCancellationStore, ReservationCancellationStore>();
        services.AddScoped<IReservationReadStore, ReservationReadStore>();
        services.AddScoped<IListingPhotoStore, ListingPhotoStore>();
        services.AddScoped<IPublicListingStore, PublicListingStore>();
        services.AddScoped<IPurchaseQuoteStore, PurchaseQuoteStore>();
        services.AddScoped<IPurchaseConfigurationStore, PurchaseConfigurationStore>();
        services.AddScoped<IPurchaseSettingsAdministrationStore, PurchaseSettingsAdministrationStore>();
        services.AddScoped<IRetentionSettingsStore, RetentionSettingsStore>();
        services.AddScoped<IPromoAdministrationStore, PromoAdministrationStore>();
        services.AddScoped<IWarrantyAdministrationStore, WarrantyAdministrationStore>();
        services.AddScoped<Application.Sales.ISaleCommandStore, Sales.SaleCommandStore>();
        services.AddScoped<Application.Sales.ISaleReadStore, Sales.SaleReadStore>();
        services.AddScoped<Application.Sales.ISaleDocumentStore, Sales.SaleDocumentStore>();
        services.AddScoped<Application.Sales.ISaleRetentionHoldStore, Sales.SaleRetentionHoldStore>();
        services.AddSingleton<Application.Sales.ISaleDocumentRenderer, Sales.SaleDocumentRenderer>();
        services.AddScoped<Application.Sales.ISaleListStore, Sales.SaleListStore>();
        services.AddScoped<Application.Sales.ISaleVehiclePhotoStore, Sales.SaleVehiclePhotoStore>();
        services.AddScoped<IListingReadStore, ListingReadStore>();
        services.AddScoped<IListingHistoryStore, ListingHistoryStore>();
        services.AddScoped<IListingEstimateStore, ListingEstimateStore>();
        services.AddScoped<IAccountSessionStore, AccountSessionStore>();
        services.AddScoped<ICustomerAccountQuery, CustomerAccountQuery>();
        services.AddScoped<IAccountProfileQuery, AccountProfileQuery>();
        services.AddScoped<IPersonQuery, PersonQuery>();
        services.AddScoped<IRecordHandlerQuery, RecordHandlerQuery>();
        services.AddScoped<IAccountPictureStore, AccountPictureStore>();
        services.AddScoped<IAccountUsernameStore, AccountUsernameStore>();
        services.AddScoped<ICustomerClosureStore, CustomerClosureStore>();
        services.AddScoped<IAccountErasureStore, AccountErasureStore>();
        services.AddScoped<IStaffProvisioningStore, StaffProvisioningStore>();
        services.AddScoped<IBranchProvisioningStore, BranchProvisioningStore>();
        services.AddScoped<IEmailVerificationStore, EmailVerificationStore>();
        services.AddSingleton<EmailVerificationToken>();
        services.AddScoped<IStaffInvitationStore, StaffInvitationStore>();
        services.AddScoped<IStaffAccountDirectory, Accounts.StaffAccountDirectory>();
        services.AddScoped<Application.Performance.IPerformanceSummaryQuery, Performance.PerformanceSummaryQuery>();
        services.AddScoped<Application.Performance.IPerformanceStockReads, Performance.PerformanceStockReads>();
        services.AddScoped<Application.Performance.IPerformanceWorkloadReads, Performance.PerformanceWorkloadReads>();
        services.AddScoped<Application.Performance.IPerformanceMilestoneQuery, Performance.PerformanceMilestoneQuery>();
        services.AddScoped<Application.Performance.IPerformanceSalesTeamReads, Performance.PerformanceSalesTeamReads>();
        services.AddScoped<Application.Performance.IPerformanceWorkshopTeamReads, Performance.PerformanceWorkshopTeamReads>();
        services.AddScoped<ICustomerAccountDirectory, Accounts.CustomerAccountDirectory>();
        services.AddScoped<IStaffAssistanceQuery, Accounts.StaffAssistanceQuery>();
        services.AddScoped<IAccountSuspensionStore, Accounts.AccountSuspensionStore>();
        services.AddScoped<IAccountEditStore, Accounts.AccountEditStore>();
        services.AddScoped<IStaffAccountAdministrationStore, Accounts.StaffAccountAdministrationStore>();
        services.AddScoped<ICustomerSupportStore, Accounts.CustomerSupportStore>();
        services.AddSingleton<StaffInvitationToken>();
        services.AddScoped<IPasswordRecoveryStore, PasswordRecoveryStore>();
        services.AddSingleton<PasswordResetToken>();
        services.AddScoped<IPublicBranchStore, PublicBranchStore>();
        services.AddScoped<IPurchaseGuideQuery, PurchaseGuideQuery>();
        services.AddScoped<IIntakePhotoStore, IntakePhotoStore>();
        services.AddScoped<IPhotoTransferLock, PhotoTransferLock>();
        services.AddScoped<IIntakeBookingStore, IntakeBookingStore>();
        services.AddScoped<Application.TestDrives.ITestDriveBookingStore, TestDrives.TestDriveBookingStore>();
        services.AddScoped<Application.TestDrives.ITestDriveCancellationStore, TestDrives.TestDriveCancellationStore>();
        services.AddScoped<Application.TestDrives.ITestDriveAssignmentStore, TestDrives.TestDriveAssignmentStore>();
        services.AddScoped<Application.TestDrives.ITestDriveHandlingStore, TestDrives.TestDriveHandlingStore>();
        services.AddScoped<Application.TestDrives.ITestDriveReadStore, TestDrives.TestDriveReadStore>();
        services.AddScoped<Application.TestDrives.IStaffTestDriveReadStore, TestDrives.StaffTestDriveReadStore>();
        services.AddScoped<IAppointmentHistoryStore, AppointmentHistoryStore>();
        services.AddScoped<IAppointmentCancellationStore, AppointmentCancellationStore>();
        services.AddScoped<IAppointmentCheckInStore, AppointmentCheckInStore>();
        services.AddScoped<IAppointmentAssignmentStore, AppointmentAssignmentStore>();
        services.AddScoped<IInspectionAssignmentStore, InspectionAssignmentStore>();
        services.AddScoped<IInspectionDraftSaveStore, InspectionDraftSaveStore>();
        services.AddScoped<IInspectionReportStore, InspectionReportStore>();
        services.AddSingleton<IInspectionReportDocumentRenderer, InspectionReportDocumentRenderer>();
        services.AddScoped<IInspectionSignoffStore, InspectionSignoffStore>();
        services.AddScoped<IInspectionReopeningStore, InspectionReopeningStore>();
        services.AddScoped<IInspectionRepairRequestStore, InspectionRepairRequestStore>();
        services.AddScoped<IInspectionRepairApprovalStore, InspectionRepairApprovalStore>();
        services.AddScoped<IInspectionRepairCompletionStore, InspectionRepairCompletionStore>();
        services.AddScoped<IInspectionHistoryStore, InspectionHistoryStore>();
        services.AddScoped<IInspectionWorkspaceStore, InspectionWorkspaceStore>();
        services.AddScoped<IInspectionQueueStore, InspectionQueueStore>();
        services.AddScoped<IWorkshopQueueStore, WorkshopQueueStore>();
        services.AddScoped<IInspectionPhotoStore, InspectionPhotoStore>();
        services.AddScoped<IStaffWorklistStore, StaffWorklistStore>();
        services.AddScoped<Application.WorkCalendar.IWorkCalendarStore, WorkCalendar.WorkCalendarStore>();
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
