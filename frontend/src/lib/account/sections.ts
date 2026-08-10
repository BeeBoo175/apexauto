export const accountSections = [
  { id: "reservations", label: "Reservations" },
  { id: "appointments", label: "Selling appointments" },
  { id: "test-drives", label: "Test drives" },
  { id: "account-details", label: "Account details" },
] as const;

export type AccountSection = (typeof accountSections)[number]["id"];

export function sectionFromParam(value: string | string[] | undefined): AccountSection | undefined {
  const token = Array.isArray(value) ? value[0] : value;
  return accountSections.find(({ id }) => id === token)?.id;
}

export function sectionFromHash(hash: string): AccountSection | undefined {
  return accountSections.find(({ id }) => `#${id}` === hash)?.id;
}
