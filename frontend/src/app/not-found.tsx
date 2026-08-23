import Link from "next/link";
import { buttonClassName } from "@/components/ui/Button";
import { StatusPage } from "@/components/ui/StatusPage";

export const metadata = {
  title: "Page not found | ApexAuto",
  robots: { index: false, follow: false },
};

export default function NotFound() {
  return (
    <StatusPage
      code="404"
      icon="search"
      title="We can't find that page"
      actions={
        <Link href="/" className={buttonClassName("secondary")}>
          Back to home
        </Link>
      }
    >
      The address may have changed or the page may have been removed.
    </StatusPage>
  );
}
