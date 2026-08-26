import { ReloadNotice } from "@/components/ui/ReloadNotice";
import { StaffCommandBar } from "./StaffPage";

// The page body for a workspace whose first read failed: it keeps the page title bar and fills the whole content area
// with the shared reload notice.
export function StaffReloadNotice({ title }: { title: string }) {
  return (
    <>
      <StaffCommandBar title={title} />
      <ReloadNotice subject={title} />
    </>
  );
}
