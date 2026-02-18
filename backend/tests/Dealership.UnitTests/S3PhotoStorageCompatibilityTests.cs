using Dealership.Domain.Photos;
using Dealership.Application.Photos;
using Dealership.Infrastructure.Storage;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dealership.UnitTests;

public sealed class S3PhotoStorageCompatibilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task HttpsUploadUsesPortableHeadersAndVerifiesOriginalBytesBeforeSuccess(bool corruptReadBack,
        bool inspectionObject)
    {
        byte[] bytes = "validated photo contents"u8.ToArray();
        var intakePhoto = new IntakePhoto(Guid.NewGuid(), Guid.NewGuid(), PhotoAngle.Front,
            new PhotoContent("image/jpeg", bytes.Length, Convert.ToHexStringLower(SHA256.HashData(bytes)), 8, 8),
            DateTimeOffset.UtcNow, TimeSpan.FromHours(24));
        IPhotoObject photo = inspectionObject
            ? new EvidenceObject($"inspection/photos/{Guid.NewGuid():N}", intakePhoto.ContentType,
                intakePhoto.Bytes, intakePhoto.Sha256)
            : intakePhoto;
        var settings = Settings();
        using var handler = new StorageHandler(bytes, corruptReadBack);
        using var client = new AmazonS3Client(new BasicAWSCredentials(settings.AccessKey, settings.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = settings.ServiceURL,
                AuthenticationRegion = settings.Region,
                ForcePathStyle = settings.ForcePathStyle,
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
                MaxErrorRetry = 0,
                HttpClientFactory = new TestClientFactory(() => new HttpClient(handler, disposeHandler: false))
            });
        var storage = new S3PhotoStorage(client, settings);
        using var input = new MemoryStream(bytes);

        if (corruptReadBack)
        {
            await Assert.ThrowsAsync<PhotoStorageException>(() => storage.PutAsync(photo, input, CancellationToken.None));
        }
        else
        {
            await storage.PutAsync(photo, input, CancellationToken.None);
        }

        Assert.Equal([HttpMethod.Put, HttpMethod.Get], handler.Methods);
        Assert.Equal(bytes, handler.UploadedBytes);
        Assert.Equal($"/{settings.BucketName}/{photo.ObjectKey}", handler.UploadPath);
        Assert.Equal("*", handler.UploadHeaders["If-None-Match"]);
        Assert.Equal("UNSIGNED-PAYLOAD", handler.UploadHeaders["x-amz-content-sha256"]);
        Assert.Contains("/auto/s3/aws4_request", handler.UploadHeaders["Authorization"]);
        Assert.DoesNotContain(handler.UploadHeaders.Keys, name => name.StartsWith("x-amz-checksum-", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("x-amz-sdk-checksum-algorithm", handler.UploadHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("aws-chunked", handler.UploadHeaders.Values);
        Assert.False(handler.ChunkedTransfer);
    }

    [Fact]
    public void CompositionAcceptsR2ConfigurationWithoutChangingApplicationServices()
    {
        var settings = Settings();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AWS:ServiceURL"] = settings.ServiceURL,
            ["AWS:AccessKey"] = settings.AccessKey,
            ["AWS:SecretKey"] = settings.SecretKey,
            ["AWS:BucketName"] = settings.BucketName,
            ["AWS:Region"] = "auto",
            ["AWS:ForcePathStyle"] = "true",
            ["IntakePhotos:UnsubmittedLifetimeHours"] = "24",
            ["IntakePhotos:CancelledRetentionDays"] = "30"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddS3Storage(configuration);
        services.AddIntakeMedia(configuration);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IAmazonS3>();
        Assert.Equal(settings.ServiceURL, client.Config.ServiceURL.TrimEnd('/'));
        Assert.Equal("auto", client.Config.AuthenticationRegion);
        Assert.Equal(RequestChecksumCalculation.WHEN_REQUIRED, client.Config.RequestChecksumCalculation);
        Assert.Equal(ResponseChecksumValidation.WHEN_REQUIRED, client.Config.ResponseChecksumValidation);
        Assert.IsType<S3PhotoStorage>(provider.GetRequiredService<IPhotoStorage>());
    }

    private static PhotoStorageSettings Settings()
    {
        return new PhotoStorageSettings
        {
            ServiceURL = "https://example-account.r2.cloudflarestorage.com",
            AccessKey = "test-access",
            SecretKey = "test-secret",
            BucketName = "apexauto-photos",
            Region = "auto",
            ForcePathStyle = true
        };
    }

    private sealed record EvidenceObject(string ObjectKey, string ContentType, long Bytes, string Sha256) : IPhotoObject;

    private sealed class TestClientFactory(Func<HttpClient> createClient) : Amazon.Runtime.HttpClientFactory
    {
        public override HttpClient CreateHttpClient(IClientConfig clientConfig) => createClient();
    }

    private sealed class StorageHandler(byte[] bytes, bool corruptReadBack) : HttpMessageHandler
    {
        public List<HttpMethod> Methods { get; } = [];
        public Dictionary<string, string> UploadHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public byte[]? UploadedBytes { get; private set; }
        public string? UploadPath { get; private set; }
        public bool ChunkedTransfer { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Methods.Add(request.Method);

            if (request.Method == HttpMethod.Put)
            {
                UploadedBytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                UploadPath = request.RequestUri!.AbsolutePath;
                ChunkedTransfer = request.Headers.TransferEncodingChunked == true;

                foreach (var header in request.Headers.Concat(request.Content.Headers))
                {
                    UploadHeaders[header.Key] = string.Join(',', header.Value);
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
                response.Headers.ETag = new EntityTagHeaderValue("\"stored-photo\"");
                return response;
            }

            var content = (byte[])bytes.Clone();

            if (corruptReadBack)
            {
                content[^1] ^= 1;
            }

            var body = new ByteArrayContent(content);
            body.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = body };
        }
    }
}
