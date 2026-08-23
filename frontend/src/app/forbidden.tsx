import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { StatusPage } from "@/components/ui/StatusPage";

export const metadata = { title: "No access | ApexAuto" };

// Shown for every page a signed-in account may not open. It never names a role or rule, so it reveals nothing about
// who can open the page.
export default function Forbidden() {
  return (
    <StatusPage
      code="403"
      icon="shield-check"
      title="You don't have access to this page"
      actions={
        <Link href="/dashboard" className={buttonClassName("secondary")}>
          Return to your workspace
        </Link>
      }
    >
      Your account can&apos;t open this page.
    </StatusPage>
  );
}
