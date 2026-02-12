using Dealership.Application.Accounts;

namespace Dealership.Application.Intake;

public static class StaffExportLimits
{
    public const int ImmediateRows = 10000;
    public const long BackgroundBytes = 64 * 1024 * 1024;
    public static readonly TimeSpan BackgroundDeadline = TimeSpan.FromSeconds(120);
}

public interface IStaffExportWriter
{
    Task<bool> WriteAsync(CurrentActor actor, StaffWorklistQuery query, Stream destination,
        CancellationToken cancellationToken);
}

public interface IStaffExportWorker
{
    Task<Stream?> GenerateAsync(CurrentActor actor, StaffWorklistQuery query, CancellationToken cancellationToken);
}

public sealed class StaffExportBusyException : Exception
{
}

public sealed class StaffExportTooLargeException : IOException
{
}
