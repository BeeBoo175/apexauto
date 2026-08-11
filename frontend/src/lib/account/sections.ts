export const accountSections = [
  { id: "reservations", label: "Reservations" },
  { id: "appointments", label: "Selling appointments" },
  { id: "test-drives", label: "Test drives" },
  { id: "details", label: "Account details" },
] as const;

export type AccountSection = (typeof accountSections)[number]["id"];

const accountPath = "/account";

// The first section is the workspace root, so its address carries no section segment.
export function accountSectionPath(section: AccountSection) {
  return section === "reservations" ? accountPath : `${accountPath}/${section}`;
}

export function sectionFromSegments(segments: string[] | undefined): AccountSection | null {
  if (!segments || segments.length === 0) {
    return "reservations";
  }
  if (segments.length > 1) {
    return null;
  }
  const named = accountSections.find(({ id }) => id === segments[0]);
  return named && named.id !== "reservations" ? named.id : null;
}

export function sectionFromPathname(pathname: string): AccountSection {
  const segments = pathname.split("/").filter(Boolean).slice(1);
  return sectionFromSegments(segments) ?? "reservations";
}
