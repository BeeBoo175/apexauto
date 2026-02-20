using Dealership.Application.Common;
using Dealership.Application.Inspections;
using Dealership.Application.Intake;

namespace Dealership.UnitTests;

public sealed class ListQueryTests
{
    [Theory]
    [InlineData(10, true)]
    [InlineData(25, true)]
    [InlineData(50, true)]
    [InlineData(1, false)]
    [InlineData(20, false)]
    [InlineData(0, false)]
    [InlineData(51, false)]
    public void AllListsUseTheSamePageSizePolicy(int size, bool accepted)
    {
        Assert.Equal(accepted, Pagination.IsValid(int.MaxValue, size));
        Assert.Equal(accepted, StaffWorklistQuery.Parse(null, null, null, null, 1, size) is not null);
        Assert.Equal(accepted, AppointmentHistoryQuery.Parse(null, null, 1, size) is not null);
        Assert.Equal(accepted, InspectionQueueFilter.Parse(null, null, null, pageSize: size) is not null);
        Assert.Equal(accepted, new InspectionHistoryQuery(1, size).IsValid);
    }

    [Fact]
    public void EveryDateFilteredListRejectsUnrepresentableMalaysianLowerBound()
    {
        Assert.Null(StaffWorklistQuery.Parse("0001-01-01", "0001-01-01", null, null, 1, 25));
        Assert.Null(AppointmentHistoryQuery.Parse("0001-01-01", "0001-01-01", 1, 25));
        Assert.Null(InspectionQueueFilter.Parse("0001-01-01", "0001-01-01", null));
    }
}
