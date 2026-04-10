import type { Metadata } from "next";
import "./globals.css";
import styles from "./Shell.module.css";
import { ToastProvider } from "@/components/ui/Toast";
import { IconSprite } from "@/components/ui/IconSprite";
import { StaffHistoryBoundary } from "@/components/staff/StaffClientEntryBoundary";

export const metadata: Metadata = {
  title: "ApexAuto | Editorial Service Bay Dealership Platform",
  description:
    "Malaysian automotive retail and dealership management platform with verified vehicle lifecycle tracking.",
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
