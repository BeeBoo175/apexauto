using Dealership.Domain.Common;
using Dealership.Domain.Intake;

namespace Dealership.Infrastructure.Development;

public sealed class DevelopmentFixtureClock : TimeProvider
{
    private readonly Lazy<IReadOnlyList<DevelopmentIntakeMoment>> _timeline;

    public DevelopmentFixtureClock(AppointmentSchedule schedule)
    {
        var now = System.GetUtcNow();
        var anchor = UtcTime.TruncateToMicroseconds(now);
        _timeline = new Lazy<IReadOnlyList<DevelopmentIntakeMoment>>(() => DevelopmentIntakeTimeline.Create(schedule, anchor));
    }

    public IReadOnlyList<DevelopmentIntakeMoment> Timeline => _timeline.Value;

    // Only the fixture command registers this clock; canonical account verification precedes historical intake.
    public override DateTimeOffset GetUtcNow() => Timeline.Min(moment => moment.BookedAt).AddMinutes(-1);
}
