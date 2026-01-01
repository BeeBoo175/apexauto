using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Dealership.Application.Accounts;
using Dealership.Application.Intake;
using Dealership.Infrastructure.Identity;
using Dealership.Infrastructure.Intake;
using Dealership.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SkiaSharp;

namespace Dealership.IntegrationTests;

public sealed class IntakePhotoFixture : IAsyncLifetime, IAsyncDisposable
{
    private readonly string _bucket = $"aa02-photos-{Guid.NewGuid():N}";
    private AmazonS3Client? _client;
    private bool _bucketCreated;
    public RegistrationDatabase Database { get; } = new();
    public string? LastStorageFailure { get; private set; }

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var settings = configuration.GetRequiredSection("AWS").Get<PhotoStorageSettings>()!;
        var endpoint = new Uri(settings.ServiceURL);

        if (!endpoint.IsLoopback || !_bucket.StartsWith("aa02-photos-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Photo tests require local S3 and an owned disposable bucket.");
        }

        _client = new AmazonS3Client(new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.ServiceURL.Replace("localhost", "127.0.0.1", StringComparison.Ordinal),
                AuthenticationRegion = settings.Region,
                ForcePathStyle = true,
                MaxErrorRetry = 0,
                Timeout = TimeSpan.FromSeconds(10)
            });

        try
        {
            await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket, UseClientRegion = true });
            _bucketCreated = true;
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public WebApplicationFactory<Program> CreateFactory(bool failAfterPut = false, TimeProvider? clock = null,
        Action<Stream>? observeDownload = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["Email:Mode"] = "Disabled", ["AWS:BucketName"] = _bucket }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<DealershipDbContext>>();
                services.RemoveAll<DealershipDbContext>();
                services.AddDbContext<DealershipDbContext>(options => options.UseNpgsql(Database.RuntimeConnectionString));

                if (clock is not null)
                {
                    services.RemoveAll<TimeProvider>();
                    services.AddSingleton(clock);
                }

                services.RemoveAll<IPhotoStorage>();
                services.AddSingleton<IPhotoStorage>(provider =>
                {
                    var settings = provider.GetRequiredService<PhotoStorageSettings>();

                    if (settings.BucketName != _bucket || !new Uri(settings.ServiceURL).IsLoopback)
                    {
                        throw new InvalidOperationException("Application storage must use this fixture's owned local bucket.");
                    }

                    return new StorageProbe(new S3PhotoStorage(provider.GetRequiredService<IAmazonS3>(), settings),
                        failAfterPut, failure => LastStorageFailure = failure, observeDownload);
                });
            });
        });
    }

    public static async Task<RegistrationRequest> RegisterAsync(HttpClient client)
    {
        var registration = new RegistrationRequest(Guid.NewGuid(), "Nur Aina Abdullah",
            $"nur.aina.{Guid.NewGuid():N}@example.invalid", "Password123!", $"940215{Random.Shared.Next(100000, 1000000)}",
            "0123456789", "18 Jalan Melati, Petaling Jaya, Selangor");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/auth/register", registration)).StatusCode);
        await LoginAsync(client, registration);
        return registration;
    }

    public static async Task LoginAsync(HttpClient client, RegistrationRequest registration)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(Guid.NewGuid(), registration.Email, registration.Password));
        var session = (await response.Content.ReadFromJsonAsync<LoginSession>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
    }

    public static async Task VerifyAsync(WebApplicationFactory<Program> factory, HttpClient client, RegistrationRequest registration)
    {
        using var scope = factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<DealershipDbContext>();
        var challenge = await database.EmailVerificationChallenges.SingleAsync(item => item.RecipientEmail == registration.Email);
        var token = scope.ServiceProvider.GetRequiredService<EmailVerificationToken>().Create(challenge);
        Assert.Equal(HttpStatusCode.NoContent,
            (await client.PostAsJsonAsync("/api/auth/email-verification", new { token })).StatusCode);
    }

    public static byte[] Image(SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg, SKColor? color = null)
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(color ?? SKColors.Silver);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    public static async Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid id, byte[] bytes,
        string angle = "Driver", string mime = "image/jpeg", bool unknownLength = false)
    {
        using HttpContent body = unknownLength ? new UnknownLengthContent(bytes) : new ByteArrayContent(bytes);
        body.Headers.ContentType = new MediaTypeHeaderValue(mime);
        return await client.PutAsync($"/api/intake/photos/{id}?angle={angle}", body);
    }

    public async Task CorruptOwnedObjectAsync(Guid id, byte[] original)
    {
        var changed = (byte[])original.Clone();
        changed[^1] ^= 1;
        using var content = new MemoryStream(changed);
        await _client!.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = $"intake/photos/{id:N}",
            InputStream = content,
            ContentType = "image/jpeg",
            UseChunkEncoding = false
        });
    }

    public async Task DisposeAsync()
    {
        if (_bucketCreated && _client is not null)
        {
            if (!_bucket.StartsWith("aa02-photos-", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Refusing unowned bucket cleanup.");
            }

            var objects = await _client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucket });

            foreach (var item in objects.S3Objects ?? [])
            {
                await _client.DeleteObjectAsync(_bucket, item.Key);
            }

            await _client.DeleteBucketAsync(_bucket);
            _bucketCreated = false;
        }

        _client?.Dispose();
        await Database.DisposeAsync();
    }

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    private sealed class StorageProbe(IPhotoStorage storage, bool failAfterPut, Action<string> observeFailure,
        Action<Stream>? observeDownload) : IPhotoStorage
    {
        public async Task PutAsync(Dealership.Domain.Intake.IntakePhoto photo, Stream content, CancellationToken cancellationToken)
        {
            try
            {
                await storage.PutAsync(photo, content, cancellationToken);
            }
            catch (PhotoStorageException exception)
            {
                var details = exception.InnerException is AmazonS3Exception provider
                    ? $"{provider.StatusCode}/{provider.ErrorCode}"
                    : exception.InnerException?.GetType().Name;
                observeFailure($"Put failed: {details}");
                throw;
            }

            if (failAfterPut)
            {
                throw new PhotoStorageException();
            }
        }

        public Task CopyToAsync(Dealership.Domain.Intake.IntakePhoto photo, Stream destination, CancellationToken cancellationToken)
        {
            observeDownload?.Invoke(destination);
            return storage.CopyToAsync(photo, destination, cancellationToken);
        }

        public Task DeleteAsync(Dealership.Domain.Intake.IntakePhoto photo, CancellationToken cancellationToken)
        {
            return storage.DeleteAsync(photo, cancellationToken);
        }
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
