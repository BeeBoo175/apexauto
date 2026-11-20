namespace Dealership.Api.Endpoints;

internal static class PhotoTemporaryFile
{
    public const int UploadMaximumBytes = 4 * 1024 * 1024;
    public const string UploadTooLargeMessage = "Photo exceeds 4 MiB.";

    public static FileStream Create()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 64 * 1024,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(Path.Combine(Path.GetTempPath(), $"apexauto-photo-{Guid.NewGuid():N}.tmp"), options);
    }

    public static async Task CopyRequestAsync(Stream source, Stream destination, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        long total = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);

            if (read == 0)
            {
                break;
            }

            total += read;

            if (total > UploadMaximumBytes)
            {
                throw new BadHttpRequestException(UploadTooLargeMessage, StatusCodes.Status413PayloadTooLarge);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        destination.Position = 0;
    }
}
