import type { DemoAccount } from "@/components/auth/DemoAccounts";

// Intentionally public portfolio credentials mirror DevelopmentFixtureManifest.
const password = "Password123!";

export const demoAccounts: readonly DemoAccount[] = [
  { label: "Customer", email: "nur.aisyah@example.invalid", password },
  { label: "Sales agent", email: "aina.sofia@example.invalid", password },
  { label: "Workshop technician", email: "azlan.rahim@example.invalid", password },
  { label: "Branch manager", email: "siti.aminah@example.invalid", password },
  { label: "General manager", email: "farah.aziz@example.invalid", password },
  { label: "Administrator", email: "muhammad.hafiz@example.invalid", password },
];
