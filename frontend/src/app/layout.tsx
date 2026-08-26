import type { Metadata } from "next";
import "./globals.css";
import styles from "./Shell.module.css";
import { ToastProvider } from "@/components/ui/Toast";
import { IconSprite } from "@/components/ui/IconSprite";
import { StaffHistoryBoundary } from "@/components/staff/StaffClientEntryBoundary";

export const metadata: Metadata = {
  title: "ApexAuto | Inspected Used Cars & Trade-In Valuation in Malaysia",
  description:
    "Browse 210-point inspected used cars with transparent RM pricing, 48-hour online reservations, instant trade-in valuations and financing estimates.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className={styles.body}>
        <IconSprite />
        <StaffHistoryBoundary>
          <ToastProvider>{children}</ToastProvider>
        </StaffHistoryBoundary>
      </body>
    </html>
  );
}
