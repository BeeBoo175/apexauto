import type { ReactNode } from "react";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { SiteFooter } from "@/components/layout/SiteFooter";
import { LoadingProgressProvider } from "@/components/ui/LoadingProgress";
import styles from "@/app/Shell.module.css";

export default function PublicLayout({ children }: { children: ReactNode }) {
  return (
    <LoadingProgressProvider initialPending={false}>
      <SiteHeader />
      <main className={styles.main}>{children}</main>
      <SiteFooter />
    </LoadingProgressProvider>
  );
}
