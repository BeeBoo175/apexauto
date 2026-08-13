import type { Metadata } from "next";
import { Suspense, type ReactNode } from "react";
import { LoadingProgress, LoadingProgressProvider } from "@/components/ui/LoadingProgress";
import styles from "@/app/Shell.module.css";

export const metadata: Metadata = {
  title: { default: "Staff workspace | ApexAuto", template: "%s | ApexAuto" },
  robots: { index: false, follow: false },
};

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <main className={`${styles.main} ${styles.dashboard}`}>
      <LoadingProgressProvider>
        <Suspense
          fallback={
            <div className={styles.dashboardPending}>
              <LoadingProgress label="Opening workspace" />
            </div>
          }
        >
          {children}
        </Suspense>
      </LoadingProgressProvider>
    </main>
  );
}
