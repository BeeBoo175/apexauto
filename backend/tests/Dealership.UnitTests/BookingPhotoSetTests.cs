using Dealership.Domain.Intake;

namespace Dealership.UnitTests;

public sealed class BookingPhotoSetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FourOwnedConfirmedAnglesProduceAnIndependentImmutableSelection()
    {
        var owner = Guid.NewGuid();
        var candidates = Candidates(owner).Reverse().ToList();
        Assert.True(BookingPhotoSet.TryCreate(owner, candidates, Now, out var selection));
        Assert.NotNull(selection);
        Assert.Equal(Enum.GetValues<PhotoAngle>(), selection.Selections.Select(photo => photo.Angle));
        Assert.Equal(candidates.OrderBy(photo => photo.Angle).Select(photo => photo.Id),
            selection.Selections.Select(photo => photo.Id));
        candidates.Clear();
        Assert.Equal(4, selection.Selections.Count);
        Assert.Throws<NotSupportedException>(() => ((IList<BookingPhotoSelection>)selection.Selections).Clear());
    }

    [Fact]
    public void MissingAndExtraPhotosAndEmptyOwnerCannotProduceASet()
    {
        var owner = Guid.NewGuid();
        var candidates = Candidates(owner);
        Assert.False(BookingPhotoSet.TryCreate(owner, null, Now, out _));
        Assert.False(BookingPhotoSet.TryCreate(owner, candidates[..3], Now, out _));
        Assert.False(BookingPhotoSet.TryCreate(owner, [.. candidates, candidates[0]], Now, out _));
        Assert.False(BookingPhotoSet.TryCreate(Guid.Empty, candidates, Now, out _));
    }

    [Theory]
    [InlineData("empty-id")]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-angle")]
    [InlineData("unknown-angle")]
    [InlineData("other-owner")]
    [InlineData("pending")]
    [InlineData("future-availability")]
    [InlineData("expired")]
    [InlineData("already-attached")]
    [InlineData("removal-pending")]
    public void IneligiblePhotoPreventsTheEntireSet(string condition)
    {
        var owner = Guid.NewGuid();
        var photos = Candidates(owner);
        photos[0] = condition switch
        {
            "empty-id" => photos[0] with { Id = Guid.Empty },
            "duplicate-id" => photos[0] with { Id = photos[1].Id },
            "duplicate-angle" => photos[0] with { Angle = photos[1].Angle },
            "unknown-angle" => photos[0] with { Angle = (PhotoAngle)999 },
            "other-owner" => photos[0] with { OwnerId = Guid.NewGuid() },
            "pending" => photos[0] with { AvailableAt = null },
            "future-availability" => photos[0] with { AvailableAt = Now.AddTicks(1) },
            "expired" => photos[0] with { ExpiresAt = Now },
            "already-attached" => photos[0] with { AlreadyAttached = true },
            "removal-pending" => photos[0] with { RemovalPending = true },
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        };

        Assert.False(BookingPhotoSet.TryCreate(owner, photos, Now, out var selection));
        Assert.Null(selection);
    }

    [Fact]
    public void AvailabilityEqualityAndTheLastTickBeforeExpiryAreEligible()
    {
        var owner = Guid.NewGuid();
        var photos = Candidates(owner);
        photos[0] = photos[0] with { AvailableAt = Now, ExpiresAt = Now.AddTicks(1) };
        Assert.True(BookingPhotoSet.TryCreate(owner, photos, Now, out _));
    }

    private static BookingPhotoCandidate[] Candidates(Guid owner)
    {
        return Enum.GetValues<PhotoAngle>().Select(angle => new BookingPhotoCandidate(Guid.NewGuid(), owner,
            angle, Now.AddMinutes(-1), Now.AddHours(24), false, false)).ToArray();
    }
}
