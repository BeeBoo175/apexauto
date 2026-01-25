using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Intake;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal sealed class DevelopmentFixtureInterruption
{
    public bool Enabled { get; set; } = true;
    public bool InterruptConfirmation { get; set; } = true;
    public bool InterruptPhoto { get; set; } = true;
    public bool InterruptCancellation { get; set; } = true;
    public bool InterruptCheckIn { get; set; } = true;
    public bool DeferCheckIn { get; set; } = true;

    public void Configure(IServiceCollection services)
    {
        services.AddScoped<IStaffProvisioningStore>(provider =>
            new StaffStore(ActivatorUtilities.CreateInstance<StaffProvisioningStore>(provider), this));
        services.AddScoped<IEmailVerificationStore>(provider =>
            new VerificationStore(ActivatorUtilities.CreateInstance<EmailVerificationStore>(provider), this));
        services.AddSingleton<IPhotoStorage>(provider =>
            new PhotoStorage(ActivatorUtilities.CreateInstance<S3PhotoStorage>(provider), this));
        services.AddScoped<IAppointmentCancellationStore>(provider =>
            new CancellationStore(ActivatorUtilities.CreateInstance<AppointmentCancellationStore>(provider), this));
        services.AddScoped<IAppointmentCheckInStore>(provider =>
            new CheckInStore(ActivatorUtilities.CreateInstance<AppointmentCheckInStore>(provider), this));
    }

    private sealed class CancellationStore(IAppointmentCancellationStore inner, DevelopmentFixtureInterruption fault)
        : IAppointmentCancellationStore
    {
        public async Task<CancellationResult> CancelAsync(Guid ownerId, Guid appointmentId, CancellationToken cancellationToken)
        {
            var result = await inner.CancelAsync(ownerId, appointmentId, cancellationToken);

            if (fault.InterruptCancellation && result.Status == CancellationStatus.Succeeded)
            {
                throw new IOException("Injected interruption after canonical cancellation committed.");
            }

            return result;
        }
    }

    private sealed class CheckInStore(IAppointmentCheckInStore inner, DevelopmentFixtureInterruption fault)
        : IAppointmentCheckInStore
    {
        public async Task<CheckInResult> CheckInAsync(Guid staffId, Guid appointmentId, CheckInEvidence evidence,
            CancellationToken cancellationToken)
        {
            if (fault.DeferCheckIn)
            {
                return new CheckInResult(CheckInStatus.Unavailable);
            }

            var result = await inner.CheckInAsync(staffId, appointmentId, evidence, cancellationToken);

            if (fault.InterruptCheckIn && result.Status == CheckInStatus.Succeeded)
            {
                throw new IOException("Injected interruption after canonical check-in committed.");
            }

            return result;
        }
    }

    private sealed class StaffStore(StaffProvisioningStore inner, DevelopmentFixtureInterruption fault) : IStaffProvisioningStore
    {
        public Task<ProvisioningResult> ProvisionStaffAsync(ValidatedStaffProvisioning request, CancellationToken cancellationToken)
        {
            if (fault.Enabled && request.Role == AccountRoles.WorkshopTechnician)
            {
                throw new IOException("Injected interruption after the first committed staff unit.");
            }

            return inner.ProvisionStaffAsync(request, cancellationToken);
        }
    }

    private sealed class VerificationStore(EmailVerificationStore inner, DevelopmentFixtureInterruption fault)
        : IEmailVerificationStore
    {
        public Task QueueInitialAsync(Guid userId, CancellationToken cancellationToken)
        {
            return inner.QueueInitialAsync(userId, cancellationToken);
        }

        public Task<VerificationResult> ResendAsync(Guid userId, Guid resendKey, CancellationToken cancellationToken)
        {
            return inner.ResendAsync(userId, resendKey, cancellationToken);
        }

        public Task<VerificationResult> ConfirmAsync(string token, CancellationToken cancellationToken)
        {
            if (fault.InterruptConfirmation)
            {
                throw new IOException("Injected interruption after canonical registration committed.");
            }

            return inner.ConfirmAsync(token, cancellationToken);
        }
    }

    private sealed class PhotoStorage(IPhotoStorage inner, DevelopmentFixtureInterruption fault) : IPhotoStorage
    {
        public async Task PutAsync(IntakePhoto photo, Stream content, CancellationToken cancellationToken)
        {
            await inner.PutAsync(photo, content, cancellationToken);

            if (fault.InterruptPhoto)
            {
                throw new IOException("Injected interruption after the immutable object was stored.");
            }
        }

        public Task CopyToAsync(IntakePhoto photo, Stream destination, CancellationToken cancellationToken)
        {
            return inner.CopyToAsync(photo, destination, cancellationToken);
        }

        public Task DeleteAsync(IntakePhoto photo, CancellationToken cancellationToken)
        {
            return inner.DeleteAsync(photo, cancellationToken);
        }
    }
}
