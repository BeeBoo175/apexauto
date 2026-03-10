using System.Text.Json;
using Dealership.Application.Inspections;

namespace Dealership.Infrastructure.Inspections;

internal sealed record WorkshopQueueCursor(int Version, Guid ActorId, Guid? BranchId,
    WorkshopTask Task, string Search, int PageSize, DateTimeOffset Since, Guid VehicleId)
{
    public string Encode() => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(this));

    public bool Matches(Guid actorId, Guid? branchId, InspectionQueueFilter filter, WorkshopTask task)
    {
        return Version == 1 && ActorId == actorId && BranchId == branchId && Task == task &&
            Search == filter.Search && PageSize == filter.PageSize &&
            Since != default && Since.Offset == TimeSpan.Zero && VehicleId != Guid.Empty;
    }

    public static WorkshopQueueCursor? Parse(string value)
    {
        if (value.Length is 0 or > 2048)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WorkshopQueueCursor>(Convert.FromBase64String(value));
        }
        catch (Exception error) when (error is FormatException or JsonException)
        {
            return null;
        }
    }
}
