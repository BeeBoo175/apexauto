import type { Metadata } from "next";
import type { ReactNode } from "react";
import { LoadingProgressProvider } from "@/components/ui/LoadingProgress";
import styles from "@/app/Shell.module.css";

export const metadata: Metadata = {
  title: "Staff workspace | ApexAuto",
  robots: { index: false, follow: false },
};

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return (
    <main className={styles.main}>
      <LoadingProgressProvider>{children}</LoadingProgressProvider>
    </main>
  );
}
