import type { ReactNode } from "react";
import { LoadingProgressProvider } from "@/components/ui/LoadingProgress";

// Like the dashboard, the account reads its data in the browser, so its bar is part of the first document: it is
// visible with the loading shell and finishes when the opened section's first read settles.
export default function AccountLayout({ children }: { children: ReactNode }) {
  return (
    <LoadingProgressProvider initialLabel="Opening your account">
      {children}
    </LoadingProgressProvider>
  );
}
