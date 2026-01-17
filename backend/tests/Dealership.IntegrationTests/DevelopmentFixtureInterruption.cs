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

    public void Configure(IServiceCollection services)
    {
        services.AddScoped<IStaffProvisioningStore>(provider =>
            new StaffStore(ActivatorUtilities.CreateInstance<StaffProvisioningStore>(provider), this));
        services.AddScoped<IEmailVerificationStore>(provider =>
            new VerificationStore(ActivatorUtilities.CreateInstance<EmailVerificationStore>(provider), this));
        services.AddSingleton<IPhotoStorage>(provider =>
            new PhotoStorage(ActivatorUtilities.CreateInstance<S3PhotoStorage>(provider), this));
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
