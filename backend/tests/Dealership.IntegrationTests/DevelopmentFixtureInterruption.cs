using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.IntegrationTests;

internal sealed class DevelopmentFixtureInterruption
{
    public bool Enabled { get; set; } = true;
    private int _uploads;

    public void Configure(IServiceCollection services)
    {
        services.AddSingleton<IPhotoStorage>(provider =>
            new PhotoStorage(ActivatorUtilities.CreateInstance<S3PhotoStorage>(provider), this));
    }

    private sealed class PhotoStorage(IPhotoStorage inner, DevelopmentFixtureInterruption fault) : IPhotoStorage
    {
        public async Task PutAsync(IntakePhoto photo, Stream content, CancellationToken cancellationToken)
        {
            await inner.PutAsync(photo, content, cancellationToken);
            if (fault.Enabled && Interlocked.Increment(ref fault._uploads) == 5)
            {
                throw new IOException("Injected lost acknowledgement after the first appointment bundle was inserted.");
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
