import { buttonClassName } from "@/components/ui/Button";

// Starts work for a customer in the public journey, where sales staff choose the customer before they act.
// A plain link leaves the dashboard frame on purpose, because the journey is the public page.
export function AssistedStartLink({ href, label }: { href: string; label: string }) {
  return (
    <a href={href} className={buttonClassName("secondary", "compact")}>
      {label}
    </a>
  );
}
