using Dealership.Application.Accounts;

namespace Dealership.UnitTests;

public sealed class AccountChallengePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void VerificationCooldownIncludesEveryInstantBeforeItsBoundary()
    {
        Assert.Null(EmailVerificationPolicy.ResendDelay(Now, []));
        Assert.Null(EmailVerificationPolicy.ResendDelay(Now, [Now.AddMinutes(-1)]));
        Assert.Equal(TimeSpan.FromTicks(1),
            EmailVerificationPolicy.ResendDelay(Now, [Now.AddMinutes(-1).AddTicks(1)]));
        Assert.Equal(TimeSpan.FromHours(24), EmailVerificationPolicy.Lifetime);
    }

    [Fact]
    public void VerificationCooldownPrecedesQuotaAndQuotaUsesOldestRecentResend()
    {
        Assert.Null(EmailVerificationPolicy.ResendDelay(Now, [Now.AddMinutes(-1), Now.AddMinutes(-2)]));
        Assert.Equal(TimeSpan.FromMinutes(57), EmailVerificationPolicy.ResendDelay(Now,
            [Now.AddMinutes(-1), Now.AddMinutes(-2), Now.AddMinutes(-3)]));
        Assert.Equal(TimeSpan.FromSeconds(30), EmailVerificationPolicy.ResendDelay(Now,
            [Now.AddSeconds(-30), Now.AddMinutes(-2), Now.AddMinutes(-3)]));
        Assert.Equal(TimeSpan.FromTicks(1), EmailVerificationPolicy.ResendDelay(Now,
            [Now.AddMinutes(-1), Now.AddMinutes(-2), Now.AddHours(-1).AddTicks(1)]));
    }

    [Fact]
    public void RecoveryCooldownAndQuotaRetainExactAcceptanceBoundaries()
    {
        Assert.True(PasswordRecoveryPolicy.CanRequest(Now, []));
        Assert.True(PasswordRecoveryPolicy.CanRequest(Now, [Now.AddMinutes(-1)]));
        Assert.False(PasswordRecoveryPolicy.CanRequest(Now, [Now.AddMinutes(-1).AddTicks(1)]));
        Assert.True(PasswordRecoveryPolicy.CanRequest(Now, [Now.AddMinutes(-1), Now.AddMinutes(-2)]));
        Assert.False(PasswordRecoveryPolicy.CanRequest(Now,
            [Now.AddMinutes(-1), Now.AddMinutes(-2), Now.AddHours(-1).AddTicks(1)]));
        Assert.Equal(TimeSpan.FromHours(1), PasswordRecoveryPolicy.Lifetime);
        Assert.Equal(TimeSpan.FromHours(1), PasswordRecoveryPolicy.RequestWindow);
        Assert.Equal(TimeSpan.FromHours(1), EmailVerificationPolicy.ResendWindow);
    }
}
