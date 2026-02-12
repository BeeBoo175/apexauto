using Dealership.Application.Intake;
using System.Runtime.InteropServices;

namespace Dealership.Infrastructure.Intake;

internal sealed class TemporaryExportFile() : FileStream(
    Path.Combine(Path.GetTempPath(), $"apexauto-export-{Guid.NewGuid():N}.csv"),
    CreateOptions())
{
    private static FileStreamOptions CreateOptions()
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
        return options;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (Position + buffer.Length > StaffExportLimits.BackgroundBytes)
        {
            throw new StaffExportTooLargeException();
        }

        if (MemoryMarshal.TryGetArray(buffer, out var segment))
        {
            return new ValueTask(base.WriteAsync(segment.Array!, segment.Offset, segment.Count, cancellationToken));
        }

        return new ValueTask(base.WriteAsync(buffer.ToArray(), 0, buffer.Length, cancellationToken));
    }

    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        if (Position + count > StaffExportLimits.BackgroundBytes)
        {
            throw new StaffExportTooLargeException();
        }

        return base.WriteAsync(buffer, offset, count, cancellationToken);
    }
}
