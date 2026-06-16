import type { DemoAccount } from "@/components/auth/DemoAccounts";

// Intentionally public portfolio credentials mirror DevelopmentFixtureManifest.
const password = "Password123!";

export const demoAccounts: readonly DemoAccount[] = [
  {
    group: "Customer",
    label: "Customer",
    description: "Buyer with settled and delivered purchases, cancelled holds and test drives",
    email: "nur.aisyah@example.invalid",
    password,
  },
  {
    group: "Sales",
    label: "Sales agent",
    description: "Glenmarie agent working claimed appointments, sales and test drives",
    email: "aina.sofia@example.invalid",
    password,
  },
  {
    group: "Sales",
    label: "Second sales agent",
    description: "Glenmarie agent who shares the branch queues and holds a separate workload",
    email: "nurul.huda@example.invalid",
    password,
  },
  {
    group: "Workshop",
    label: "Workshop technician",
    description: "Glenmarie technician with signed inspections and repair requests",
    email: "azlan.rahim@example.invalid",
    password,
  },
  {
    group: "Workshop",
    label: "Relief technician",
    description: "Glenmarie technician with a long list of open inspection drafts",
    email: "farid.hakim@example.invalid",
    password,
  },
  {
    group: "Management",
    label: "Branch manager",
    description: "Glenmarie manager who assigns work, approves sales and oversees the branch",
    email: "siti.aminah@example.invalid",
    password,
  },
  {
    group: "Management",
    label: "General manager",
    description: "Oversees every branch and the listings, holds and sales across them",
    email: "farah.aziz@example.invalid",
    password,
  },
  {
    group: "Administration",
    label: "Administrator",
    description: "Maintains promotions, warranty packages and purchase settings",
    email: "muhammad.hafiz@example.invalid",
    password,
  },
];
