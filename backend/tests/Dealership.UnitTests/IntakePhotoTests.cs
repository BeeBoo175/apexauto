using Dealership.Infrastructure.Photos;
using Dealership.Domain.Photos;
using Dealership.Infrastructure.Storage;
using System.Security.Cryptography;
using Dealership.Domain.Intake;
using Dealership.Infrastructure.Intake;
using SkiaSharp;

namespace Dealership.UnitTests;

public sealed class IntakePhotoTests
{
    [Theory]
    [InlineData(SKEncodedImageFormat.Jpeg, "image/jpeg")]
    [InlineData(SKEncodedImageFormat.Png, "image/png")]
    [InlineData(SKEncodedImageFormat.Webp, "image/webp")]
    public async Task DecoderValidatesActualContentsAndPreservesOriginalDigest(SKEncodedImageFormat format, string mime)
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.Silver);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        var bytes = encoded.ToArray();
        using var stream = new MemoryStream(bytes);
        using var validator = new PhotoImageValidator(64);
        var result = await validator.ValidateAsync(stream, mime, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal((8, 8), (result.Width, result.Height));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), result.Sha256);
        Assert.Equal(bytes.Length, result.Bytes);
        Assert.True(stream.CanRead);

        using var limited = new PhotoImageValidator(63);
        Assert.Null(await limited.ValidateAsync(stream, mime, CancellationToken.None));
        Assert.Null(await validator.ValidateAsync(stream, "image/gif", CancellationToken.None));
        var wrongMime = mime == "image/png" ? "image/jpeg" : "image/png";
        Assert.Null(await validator.ValidateAsync(stream, wrongMime, CancellationToken.None));
        using var truncated = new MemoryStream(bytes[..(bytes.Length / 2)]);
        Assert.Null(await validator.ValidateAsync(truncated, mime, CancellationToken.None));
    }

    [Fact]
    public async Task EmptyOversizedAndMasqueradingContentsAreRejected()
    {
        using var validator = new PhotoImageValidator();
        using var empty = new MemoryStream();
        using var oversized = new MemoryStream(new byte[PhotoContent.MaximumBytes + 1]);
        using var masquerade = new MemoryStream("this is not an exterior photograph"u8.ToArray());
        Assert.Null(await validator.ValidateAsync(empty, "image/png", CancellationToken.None));
        Assert.Null(await validator.ValidateAsync(oversized, "image/png", CancellationToken.None));
        Assert.Null(await validator.ValidateAsync(masquerade, "image/png", CancellationToken.None));
    }

    [Fact]
    public void StorageConfigurationRequiresCredentialsAndExplicitInsecureNetworkOptIn()
    {
        var settings = new PhotoStorageSettings
        {
            ServiceURL = "https://storage.example.invalid",
            AccessKey = "test-access",
            SecretKey = "test-secret",
            BucketName = "apexauto-photos",
            Region = "ap-southeast-1"
        };
        settings.Validate();
        settings.ServiceURL = "http://storage.example.invalid";
        Assert.Throws<InvalidOperationException>(settings.Validate);
        settings.AllowInsecureHttp = true;
        settings.Validate();
        settings.ServiceURL = "https://user:password@storage.example.invalid";
        Assert.Throws<InvalidOperationException>(settings.Validate);
        settings.AllowInsecureHttp = false;
        settings.ServiceURL = "http://127.0.0.1:9090";
        settings.Validate();
        settings.SecretKey = string.Empty;
        Assert.Throws<InvalidOperationException>(settings.Validate);
    }

    [Fact]
    public void PhotoIdentityExpiryAndFirstAvailableReceiptAreStable()
    {
        var now = DateTimeOffset.UtcNow;
        var owner = Guid.NewGuid();
        var content = new PhotoContent("image/jpeg", 100, new string('a', 64), 8, 8);
        var photo = new IntakePhoto(Guid.NewGuid(), owner, PhotoAngle.Driver, content, now, TimeSpan.FromHours(24));
        Assert.True(photo.Matches(owner, PhotoAngle.Driver, content));
        Assert.False(photo.Matches(Guid.NewGuid(), PhotoAngle.Driver, content));
        Assert.False(photo.Matches(owner, PhotoAngle.Rear, content));
        Assert.False(photo.Matches(owner, PhotoAngle.Driver, content with { Sha256 = new string('b', 64) }));
        Assert.False(photo.IsExpired(photo.ExpiresAt.AddTicks(-1)));
        Assert.True(photo.IsExpired(photo.ExpiresAt));
        photo.ConfirmAvailable(now.AddMinutes(1));
        photo.ConfirmAvailable(now.AddMinutes(2));
        Assert.Equal(now.AddMinutes(1), photo.AvailableAt);
        Assert.Throws<InvalidOperationException>(() => photo.ConfirmAvailable(photo.ExpiresAt));
        Assert.Throws<ArgumentException>(() => new IntakePhoto(Guid.Empty, owner, PhotoAngle.Driver, content, now,
            TimeSpan.FromHours(24)));
    }
}
