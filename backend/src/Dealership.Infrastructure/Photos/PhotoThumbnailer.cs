using Dealership.Application.Photos;
using Dealership.Domain.Photos;
using System.Security.Cryptography;
using SkiaSharp;

namespace Dealership.Infrastructure.Photos;

public sealed class PhotoThumbnailer : IPhotoThumbnailer
{
    private const int JpegQuality = 85;

    public async Task<PhotoContent?> CreateAsync(Stream source, Stream destination, int edge,
        CancellationToken cancellationToken)
    {
        source.Position = 0;
        using var original = SKBitmap.Decode(source);

        if (original is null)
        {
            return null;
        }

        var side = Math.Min(original.Width, original.Height);
        var crop = SKRect.Create((original.Width - side) / 2f, (original.Height - side) / 2f, side, side);
        using var surface = SKSurface.Create(new SKImageInfo(edge, edge, SKColorType.Rgba8888, SKAlphaType.Premul));
        // JPEG has no alpha, so transparent areas of a PNG or WebP picture sit on white.
        surface.Canvas.Clear(SKColors.White);
        using var image = SKImage.FromBitmap(original);
        surface.Canvas.DrawImage(image, crop, SKRect.Create(edge, edge),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), null);
        using var encoded = surface.Snapshot().Encode(SKEncodedImageFormat.Jpeg, JpegQuality);

        cancellationToken.ThrowIfCancellationRequested();
        var bytes = encoded.ToArray();
        await destination.WriteAsync(bytes, cancellationToken);
        var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new PhotoContent("image/jpeg", bytes.Length, hash, edge, edge);
    }
}
