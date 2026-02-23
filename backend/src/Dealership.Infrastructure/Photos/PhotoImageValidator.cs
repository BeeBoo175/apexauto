using Dealership.Domain.Photos;
using Dealership.Application.Photos;
using System.Security.Cryptography;
using SkiaSharp;

namespace Dealership.Infrastructure.Photos;

public sealed class PhotoImageValidator(long maximumPixels = PhotoImageValidator.MaximumPixels) : IPhotoImageValidator, IDisposable
{
    public const long MaximumPixels = 50_000_000;
    private readonly SemaphoreSlim _decoder = new(1, 1);

    public async Task<PhotoContent?> ValidateAsync(Stream content, string contentType, CancellationToken cancellationToken)
    {
        if (!content.CanSeek || content.Length is < 1 or > PhotoContent.MaximumBytes)
        {
            return null;
        }

        await _decoder.WaitAsync(cancellationToken);

        try
        {
            content.Position = 0;
            using var codec = SKCodec.Create(new SKManagedStream(content, disposeManagedStream: false));

            if (codec is null || ContentType(codec.EncodedFormat) != contentType ||
                (long)codec.Info.Width * codec.Info.Height > maximumPixels || codec.FrameCount > 1)
            {
                return null;
            }

            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap();

            if (!bitmap.TryAllocPixels(info) || codec.GetPixels(info, bitmap.GetPixels()) != SKCodecResult.Success)
            {
                return null;
            }

            cancellationToken.ThrowIfCancellationRequested();
            content.Position = 0;
            var hash = await SHA256.HashDataAsync(content, cancellationToken);
            return new PhotoContent(contentType, content.Length, Convert.ToHexStringLower(hash), info.Width, info.Height);
        }
        finally
        {
            _decoder.Release();
        }
    }

    public void Dispose() => _decoder.Dispose();

    private static string? ContentType(SKEncodedImageFormat format)
    {
        return format switch
        {
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Webp => "image/webp",
            _ => null
        };
    }
}
