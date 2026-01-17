using Dealership.Domain.Intake;
using Microsoft.Extensions.Configuration;

namespace Dealership.Infrastructure.Configuration;

public sealed class InspectionScheduleConfiguration
{
    public TimeOnly OpensAt { get; set; }

    public TimeOnly ClosesAt { get; set; }

    public int SlotMinutes { get; set; }

    public int HorizonDays { get; set; }

    public int NoticeMinutes { get; set; }

    public DayOfWeek[] OpenDays { get; set; } = [];

    public static InspectionSchedule Read(IConfiguration configuration)
    {
        var settings = configuration.GetRequiredSection("InspectionSchedule").Get<InspectionScheduleConfiguration>()
            ?? throw new InvalidOperationException("InspectionSchedule configuration is required.");
        return new InspectionSchedule(settings.OpensAt, settings.ClosesAt, settings.SlotMinutes,
            settings.HorizonDays, settings.NoticeMinutes, settings.OpenDays);
    }
}
