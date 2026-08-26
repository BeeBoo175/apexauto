using System.Runtime.CompilerServices;

namespace Dealership.IntegrationTests;

// The test host runs in Development and reads the developer's .env, which may enable the real Billplz sandbox. Real
// environment variables win over that file, so every test host starts with Billplz off; a test that needs the payment
// flow installs the fake gateway or overrides the setting itself.
internal static class BillplzTestDefaults
{
    [ModuleInitializer]
    internal static void DisableBillplz()
    {
        Environment.SetEnvironmentVariable("Billplz__Enabled", "false");
    }
}