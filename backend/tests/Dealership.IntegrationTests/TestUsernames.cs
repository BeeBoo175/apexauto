namespace Dealership.IntegrationTests;

public static class TestUsernames
{
    // Disposable databases are shared within a test class, so each registration needs its own handle.
    public static string Next()
    {
        return $"user{Guid.NewGuid():N}"[..20];
    }
}
