using Dealership.Domain.Branches;
using Dealership.Domain.Customers;
using Dealership.Domain.Intake;
using Dealership.Domain.Vehicles;
using Dealership.Domain.Inspections;
using Dealership.Domain.Sales;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Inspections;
using Dealership.Infrastructure.Vehicles;
using Dealership.Infrastructure.Sales;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Dealership.Infrastructure.Persistence;

public class DealershipDbContext(DbContextOptions<DealershipDbContext> options) : IdentityUserContext<AccountUser, Guid>(options)
{
    public DbSet<Domain.TestDrives.TestDriveBooking> TestDriveBookings => Set<Domain.TestDrives.TestDriveBooking>();

    public DbSet<Domain.TestDrives.TestDriveAssignment> TestDriveAssignments => Set<Domain.TestDrives.TestDriveAssignment>();

    public DbSet<Domain.TestDrives.TestDriveStart> TestDriveStarts => Set<Domain.TestDrives.TestDriveStart>();

    public DbSet<Domain.TestDrives.TestDriveOutcome> TestDriveOutcomes => Set<Domain.TestDrives.TestDriveOutcome>();

    public DbSet<TestDrives.TestDriveOccupation> TestDriveOccupations => Set<TestDrives.TestDriveOccupation>();

    public DbSet<Domain.TestDrives.TestDriveCancellation> TestDriveCancellations => Set<Domain.TestDrives.TestDriveCancellation>();

    public DbSet<TestDrives.TestDriveMail> TestDriveMailOutbox => Set<TestDrives.TestDriveMail>();

    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();

    public DbSet<Domain.Staff.StaffProfile> StaffProfiles => Set<Domain.Staff.StaffProfile>();

    public DbSet<Domain.Accounts.AccountPicture> AccountPictures => Set<Domain.Accounts.AccountPicture>();

    public DbSet<Domain.Accounts.AccountEvent> AccountEvents => Set<Domain.Accounts.AccountEvent>();

    public DbSet<Identity.StaffInvitation> StaffInvitations => Set<Identity.StaffInvitation>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<PromoCode> PromoCodes => Set<PromoCode>();

    public DbSet<PurchaseSettings> PurchaseSettings => Set<PurchaseSettings>();

    public DbSet<WarrantyPackage> WarrantyPackages => Set<WarrantyPackage>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    public DbSet<SalesOrderEvent> SalesOrderEvents => Set<SalesOrderEvent>();

    public DbSet<SaleDeliveryMail> SaleDeliveryMailOutbox => Set<SaleDeliveryMail>();

    public DbSet<SaleDocument> SaleDocuments => Set<SaleDocument>();

    public DbSet<SaleDocumentCorrection> SaleDocumentCorrections => Set<SaleDocumentCorrection>();

    public DbSet<DealNumberCounter> DealNumberCounters => Set<DealNumberCounter>();
    public DbSet<ReservationNumberCounter> ReservationNumberCounters => Set<ReservationNumberCounter>();

    public DbSet<ReservationRelease> ReservationReleases => Set<ReservationRelease>();

    public DbSet<ReservationMail> ReservationMailOutbox => Set<ReservationMail>();

    public DbSet<ReservationDepositRequest> ReservationDepositRequests => Set<ReservationDepositRequest>();

    public DbSet<ReservationDepositMail> ReservationDepositMailOutbox => Set<ReservationDepositMail>();

    public DbSet<ReservationDepositPayment> ReservationDepositPayments => Set<ReservationDepositPayment>();

    public DbSet<ReservationRefund> ReservationRefunds => Set<ReservationRefund>();

    public DbSet<IntakePhoto> IntakePhotos => Set<IntakePhoto>();

    public DbSet<PhotoRemoval> PhotoRemovals => Set<PhotoRemoval>();

    public DbSet<IntakeAppointment> IntakeAppointments => Set<IntakeAppointment>();

    public DbSet<AppointmentAssignment> AppointmentAssignments => Set<AppointmentAssignment>();

    public DbSet<AppointmentPhoto> AppointmentPhotos => Set<AppointmentPhoto>();

    public DbSet<AppointmentMail> AppointmentMailOutbox => Set<AppointmentMail>();

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<ListingRevision> ListingRevisions => Set<ListingRevision>();

    public DbSet<ListingPublication> ListingPublications => Set<ListingPublication>();

    public DbSet<ListingWithdrawal> ListingWithdrawals => Set<ListingWithdrawal>();

    public DbSet<ListingPhoto> ListingPhotos => Set<ListingPhoto>();

    public DbSet<ListingRevisionPhoto> ListingRevisionPhotos => Set<ListingRevisionPhoto>();

    public DbSet<InspectionDraft> InspectionDrafts => Set<InspectionDraft>();

    public DbSet<InspectionAssignment> InspectionAssignments => Set<InspectionAssignment>();

    public DbSet<InspectionDraftSave> InspectionDraftSaves => Set<InspectionDraftSave>();

    public DbSet<InspectionSignedRevision> InspectionSignedRevisions => Set<InspectionSignedRevision>();

    public DbSet<InspectionReopening> InspectionReopenings => Set<InspectionReopening>();

    public DbSet<InspectionRepairRequestRecord> InspectionRepairRequests => Set<InspectionRepairRequestRecord>();

    public DbSet<InspectionRepairApproval> InspectionRepairApprovals => Set<InspectionRepairApproval>();

    public DbSet<InspectionRepairCompletion> InspectionRepairCompletions => Set<InspectionRepairCompletion>();

    public DbSet<InspectionPhoto> InspectionPhotos => Set<InspectionPhoto>();

    public DbSet<InspectionDraftPhoto> InspectionDraftPhotos => Set<InspectionDraftPhoto>();

    public DbSet<InspectionPhotoRemoval> InspectionPhotoRemovals => Set<InspectionPhotoRemoval>();

    public DbSet<AppointmentCheckIn> AppointmentCheckIns => Set<AppointmentCheckIn>();

    public DbSet<AccountSession> AccountSessions => Set<AccountSession>();

    public DbSet<EmailVerificationChallenge> EmailVerificationChallenges => Set<EmailVerificationChallenge>();

    public DbSet<PasswordResetChallenge> PasswordResetChallenges => Set<PasswordResetChallenge>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasPostgresExtension("pg_trgm");
        var testDrives = new TestDriveBookingConfiguration();
        builder.ApplyConfiguration<Domain.TestDrives.TestDriveBooking>(testDrives);
        builder.ApplyConfiguration<TestDrives.TestDriveMail>(testDrives);
        builder.ApplyConfiguration(new TestDriveCancellationConfiguration());
        builder.ApplyConfiguration(new TestDriveAssignmentConfiguration());
        var handling = new TestDriveHandlingConfiguration();
        builder.ApplyConfiguration<Domain.TestDrives.TestDriveStart>(handling);
        builder.ApplyConfiguration<Domain.TestDrives.TestDriveOutcome>(handling);
        builder.ApplyConfiguration<TestDrives.TestDriveOccupation>(handling);
        builder.ApplyConfiguration(new AccountUserConfiguration());
        builder.ApplyConfiguration(new BranchConfiguration());
        builder.ApplyConfiguration(new PromoCodeConfiguration());
        builder.ApplyConfiguration(new WarrantyPackageConfiguration());
        var sales = new SalesOrderConfiguration();
        builder.ApplyConfiguration<SalesOrder>(sales);
        builder.ApplyConfiguration<SalesOrderEvent>(sales);
        builder.ApplyConfiguration<DealNumberCounter>(sales);
        var documents = new SaleDocumentConfiguration();
        builder.ApplyConfiguration<SaleDocument>(documents);
        builder.ApplyConfiguration<ReceiptNumberCounter>(documents);
        builder.ApplyConfiguration<HandoverNumberCounter>(documents);
        builder.ApplyConfiguration(new SaleDeliveryMailConfiguration());
        builder.ApplyConfiguration(new SaleDocumentCorrectionConfiguration());
        builder.ApplyConfiguration(new PurchaseSettingsConfiguration());
        var reservations = new ReservationConfiguration();
        builder.ApplyConfiguration<Reservation>(reservations);
        builder.ApplyConfiguration<ReservationRelease>(reservations);
        builder.ApplyConfiguration<ReservationNumberCounter>(reservations);
        builder.ApplyConfiguration(new ReservationMailConfiguration());
        builder.ApplyConfiguration(new ReservationDepositRequestConfiguration());
        builder.ApplyConfiguration(new ReservationDepositMailConfiguration());
        builder.ApplyConfiguration(new ReservationDepositPaymentConfiguration());
        builder.ApplyConfiguration(new ReservationRefundConfiguration());
        builder.ApplyConfiguration(new IntakePhotoConfiguration());
        builder.ApplyConfiguration(new PhotoRemovalConfiguration());
        var booking = new IntakeBookingConfiguration();
        builder.ApplyConfiguration<IntakeAppointment>(booking);
        builder.ApplyConfiguration(new AppointmentAssignmentConfiguration());
        builder.ApplyConfiguration<AppointmentPhoto>(booking);
        builder.ApplyConfiguration(new AppointmentMailConfiguration());
        builder.ApplyConfiguration(new VehicleConfiguration());
        var listings = new ListingConfiguration();
        builder.ApplyConfiguration<ListingRevision>(listings);
        builder.ApplyConfiguration<ListingPublication>(listings);
        builder.ApplyConfiguration<ListingWithdrawal>(listings);
        var listingPhotos = new ListingPhotoConfiguration();
        builder.ApplyConfiguration<ListingPhoto>(listingPhotos);
        builder.ApplyConfiguration<ListingRevisionPhoto>(listingPhotos);
        var inspectionAssignments = new InspectionAssignmentConfiguration();
        builder.ApplyConfiguration<InspectionDraft>(inspectionAssignments);
        builder.ApplyConfiguration<InspectionAssignment>(inspectionAssignments);
        builder.ApplyConfiguration(new InspectionDraftSaveConfiguration());
        builder.ApplyConfiguration(new InspectionSignedRevisionConfiguration());
        builder.ApplyConfiguration(new InspectionReopeningConfiguration());
        builder.ApplyConfiguration(new InspectionRepairRequestConfiguration());
        builder.ApplyConfiguration(new InspectionRepairApprovalConfiguration());
        builder.ApplyConfiguration(new InspectionRepairCompletionConfiguration());
        builder.ApplyConfiguration(new InspectionPhotoConfiguration());
        var inspectionMedia = new InspectionPhotoAttachmentConfiguration();
        builder.ApplyConfiguration<InspectionDraftPhoto>(inspectionMedia);
        builder.ApplyConfiguration<InspectionPhotoRemoval>(inspectionMedia);
        builder.ApplyConfiguration(new AppointmentCheckInConfiguration());
        builder.ApplyConfiguration(new CustomerProfileConfiguration());
        builder.ApplyConfiguration(new StaffProfileConfiguration());
        builder.ApplyConfiguration(new AccountPictureConfiguration());
        builder.ApplyConfiguration(new AccountEventConfiguration());
        builder.ApplyConfiguration(new StaffInvitationConfiguration());
        builder.ApplyConfiguration(new AccountSessionConfiguration());
        builder.ApplyConfiguration(new EmailVerificationChallengeConfiguration());
        builder.ApplyConfiguration(new PasswordResetChallengeConfiguration());
    }
}
