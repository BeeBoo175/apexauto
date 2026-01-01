using System.Net;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Dealership.Application.Intake;
using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Intake;

public sealed class S3PhotoStorage(IAmazonS3 client, PhotoStorageSettings settings) : IPhotoStorage
{
    public async Task PutAsync(IntakePhoto photo, Stream content, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            var request = new PutObjectRequest
            {
                BucketName = settings.BucketName,
                Key = photo.ObjectKey,
                InputStream = content,
                AutoCloseStream = false,
                ContentType = photo.ContentType,
                IfNoneMatch = "*",
                UseChunkEncoding = false,
                DisableDefaultChecksumValidation = true,
                DisablePayloadSigning = new Uri(settings.ServiceURL).Scheme == Uri.UriSchemeHttps
            };
            await client.PutObjectAsync(request, deadline.Token);
        }
        catch (AmazonS3Exception exception) when (exception.StatusCode is HttpStatusCode.PreconditionFailed or HttpStatusCode.Conflict)
        {
            // A lost acknowledgement may leave the exact immutable object already stored.
        }
        catch (Exception exception) when (IsProviderFailure(exception, cancellationToken))
        {
            throw new PhotoStorageException(exception);
        }

        // Verify stored bytes before availability without relying on optional provider checksum support.
        await CopyToAsync(photo, Stream.Null, cancellationToken);
    }

    public async Task CopyToAsync(IntakePhoto photo, Stream destination, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            using var response = await client.GetObjectAsync(settings.BucketName, photo.ObjectKey, deadline.Token);

            if (response.ContentLength != photo.Bytes || response.Headers.ContentType != photo.ContentType)
            {
                throw new PhotoStorageException();
            }

            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            long total = 0;

            while (true)
            {
                var read = await response.ResponseStream.ReadAsync(buffer, deadline.Token);

                if (read == 0)
                {
                    break;
                }

                total += read;

                if (total > photo.Bytes)
                {
                    throw new PhotoStorageException();
                }

                hash.AppendData(buffer, 0, read);
                await destination.WriteAsync(buffer.AsMemory(0, read), deadline.Token);
            }

            if (total != photo.Bytes || Convert.ToHexStringLower(hash.GetHashAndReset()) != photo.Sha256)
            {
                throw new PhotoStorageException();
            }
        }
        catch (Exception exception) when (IsProviderFailure(exception, cancellationToken))
        {
            throw new PhotoStorageException(exception);
        }
    }

    public async Task DeleteAsync(IntakePhoto photo, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));

        try
        {
            await client.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = settings.BucketName,
                Key = photo.ObjectKey
            }, deadline.Token);
        }
        catch (Exception exception) when (IsProviderFailure(exception, cancellationToken))
        {
            throw new PhotoStorageException(exception);
        }
    }

    private static bool IsProviderFailure(Exception exception, CancellationToken cancellationToken)
    {
        return exception is AmazonS3Exception or AmazonClientException or HttpRequestException or IOException ||
            (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);
    }
}
