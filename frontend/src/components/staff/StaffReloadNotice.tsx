import { StatusPage } from "@/components/ui/StatusPage";
import { StaffCommandBar } from "./StaffPage";

// The page body for a workspace whose first read failed: it keeps the page title bar and fills the whole content area
// with the shared status page.
export function StaffReloadNotice({ title }: { title: string }) {
  return (
    <>
      <StaffCommandBar title={title} />
      <StatusPage
        tone="alert"
        code="503"
        icon="alert-triangle"
        title={`${title} could not be loaded`}
      >
        Reload the page to try again.
      </StatusPage>
    </>
  );
}
