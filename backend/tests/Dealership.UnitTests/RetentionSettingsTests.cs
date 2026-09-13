using Dealership.Domain.Accounts;

namespace Dealership.UnitTests;

public sealed class RetentionSettingsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 16, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewSettingsDefaultToThirtyDaysAndSevenYears()
    {
        var settings = new RetentionSettings(Guid.NewGuid(), Start);

        Assert.Equal((30, 30, 30, 7, 1L), (settings.SecurityArtifactDays, settings.MailDetailDays,
            settings.InvitationDays, settings.BusinessEvidenceYears, settings.Version));
    }

    [Theory]
    [InlineData(6, 30, 30, 7)]
    [InlineData(366, 30, 30, 7)]
    [InlineData(30, 6, 30, 7)]
    [InlineData(30, 30, 366, 7)]
    [InlineData(30, 30, 30, 0)]
    [InlineData(30, 30, 30, 11)]
    public void ValuesOutsideTheApprovedBoundsAreRejected(int security, int mail, int invitations, int years)
    {
        Assert.False(RetentionSettings.ValidValues(security, mail, invitations, years));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetentionSettings(Guid.NewGuid(), Start, security, mail,
            invitations, years));
    }

    [Fact]
    public void ShorteningNeedsConfirmationAndExtendingDoesNot()
    {
        var settings = new RetentionSettings(Guid.NewGuid(), Start);

        Assert.True(settings.IsShortenedBy(29, 30, 30, 7));
        Assert.False(settings.IsShortenedBy(31, 30, 30, 8));
        Assert.Throws<InvalidOperationException>(() => settings.Update(29, 30, 30, 7, false, Guid.NewGuid(), Start));
        Assert.Equal(1, settings.Version);

        var changes = settings.Update(31, 30, 30, 8, false, Guid.NewGuid(), Start);
        Assert.Equal(2, settings.Version);
        Assert.Equal(["SecurityArtifactDays", "BusinessEvidenceYears"], changes.Select(change => change.Setting));
        Assert.All(changes, change => Assert.False(change.ShorteningConfirmed));
    }

    [Fact]
    public void OnlyShortenedValuesCarryTheConfirmationInTheirReceipt()
    {
        var actor = Guid.NewGuid();
        var settings = new RetentionSettings(Guid.NewGuid(), Start);

        var changes = settings.Update(10, 40, 30, 7, true, actor, Start.AddMinutes(1));

        Assert.Equal([("SecurityArtifactDays", 30, 10, true), ("MailDetailDays", 30, 40, false)],
            changes.Select(change => (change.Setting, change.PreviousValue, change.NewValue, change.ShorteningConfirmed)));
        Assert.All(changes, change => Assert.Equal((actor, 2L), (change.ActorId, change.SettingsVersion)));
    }

    [Fact]
    public void AChangeCannotPredateTheLatestOne()
    {
        var settings = new RetentionSettings(Guid.NewGuid(), Start);

        Assert.Throws<ArgumentException>(() => settings.Update(31, 30, 30, 7, false, Guid.NewGuid(), Start.AddSeconds(-1)));
    }
}
