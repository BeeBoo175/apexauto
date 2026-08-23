import { StatusPage } from "./StatusPage";

// The one answer for content that could not be read. It never offers its own retry button: reading again is the
// browser's reload, so every section, drawer, dialog and page tells the user the same thing, in the same shared
// status layout that fills the space it replaces.
export function ReloadNotice({ subject }: { subject: string }) {
  return (
    <StatusPage
      tone="alert"
      code="503"
      icon="alert-triangle"
      title={`${subject} could not be loaded`}
    >
      Reload the page to try again.
    </StatusPage>
  );
}
