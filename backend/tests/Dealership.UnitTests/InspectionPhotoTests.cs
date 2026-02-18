using Dealership.Domain.Inspections;
using Dealership.Domain.Photos;

namespace Dealership.UnitTests;

public sealed class InspectionPhotoTests
{
    [Fact]
    public void PhotoRetainsInspectionPointAssignmentAndExactUploadIdentity()
    {
        var now = DateTimeOffset.UtcNow;
        var content = new PhotoContent("image/jpeg", 100, new string('a', 64), 8, 8);
        var photo = new InspectionPhoto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            3, "EP-004", content, now, TimeSpan.FromHours(24));
        Assert.Equal($"inspection/photos/{photo.Id:N}", photo.ObjectKey);
        Assert.IsAssignableFrom<IPhotoObject>(photo);
        Assert.True(photo.Matches(photo.InspectionId, photo.ActorId, 3, "EP-004", content));
        Assert.False(photo.Matches(Guid.NewGuid(), photo.ActorId, 3, "EP-004", content));
        Assert.False(photo.Matches(photo.InspectionId, Guid.NewGuid(), 3, "EP-004", content));
        Assert.False(photo.Matches(photo.InspectionId, photo.ActorId, 2, "EP-004", content));
        Assert.False(photo.Matches(photo.InspectionId, photo.ActorId, 3, "EP-005", content));
        Assert.False(photo.Matches(photo.InspectionId, photo.ActorId, 3, "EP-004", content with { Sha256 = new string('b', 64) }));
        Assert.Throws<InvalidOperationException>(() => photo.ConfirmAvailable(now.AddTicks(-1)));
        Assert.Throws<InvalidOperationException>(() => photo.ConfirmAvailable(photo.ExpiresAt));
        photo.ConfirmAvailable(now.AddSeconds(1));
        photo.ConfirmAvailable(now.AddSeconds(2));
        Assert.Equal(now.AddSeconds(1), photo.AvailableAt);
    }

    [Theory]
    [InlineData("identity")]
    [InlineData("inspection")]
    [InlineData("branch")]
    [InlineData("actor")]
    [InlineData("assignment")]
    [InlineData("point")]
    [InlineData("lifetime")]
    [InlineData("content")]
    public void InvalidPhotoOwnershipOrContentsAreRejected(string invalid)
    {
        var content = new PhotoContent("image/jpeg", invalid == "content" ? PhotoContent.MaximumBytes + 1 : 100,
            new string('a', 64), 8, 8);
        Assert.Throws<ArgumentException>(() => new InspectionPhoto(
            invalid == "identity" ? Guid.Empty : Guid.NewGuid(),
            invalid == "inspection" ? Guid.Empty : Guid.NewGuid(),
            invalid == "branch" ? Guid.Empty : Guid.NewGuid(),
            invalid == "actor" ? Guid.Empty : Guid.NewGuid(),
            invalid == "assignment" ? 0 : 1,
            invalid == "point" ? "EP-046" : "EP-004", content, DateTimeOffset.UtcNow,
            invalid == "lifetime" ? TimeSpan.Zero : TimeSpan.FromHours(24)));
    }
}
