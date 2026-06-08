using Dealership.Domain.Common;
using Microsoft.Extensions.Configuration;

namespace Dealership.Infrastructure.Configuration;

public sealed class AppointmentScheduleConfiguration
{
    public TimeOnly OpensAt { get; set; }

    public TimeOnly ClosesAt { get; set; }

    public int SlotMinutes { get; set; }

    public int HorizonDays { get; set; }

    public int NoticeMinutes { get; set; }

    public DayOfWeek[] OpenDays { get; set; } = [];

    public static AppointmentSchedule Read(IConfiguration configuration)
    {
        // Keep the deployed configuration key; SlotMinutes remains Intake's default duration.
        var settings = configuration.GetRequiredSection("InspectionSchedule").Get<AppointmentScheduleConfiguration>()
            ?? throw new InvalidOperationException("InspectionSchedule configuration is required.");
        return new AppointmentSchedule(settings.OpensAt, settings.ClosesAt, settings.SlotMinutes,
            settings.HorizonDays, settings.NoticeMinutes, settings.OpenDays);
    }
}
