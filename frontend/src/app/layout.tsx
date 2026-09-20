import type { Metadata } from "next";
import "./globals.css";
import styles from "./Shell.module.css";
import { ToastProvider } from "@/components/ui/Toast";
import { IconSprite } from "@/components/ui/IconSprite";
import { DemoDisclosure } from "@/components/site/DemoDisclosure";
import { StaffHistoryBoundary } from "@/components/staff/StaffClientEntryBoundary";

export const metadata: Metadata = {
  title: "ApexAuto | Inspected Used Cars & Trade-In Valuation in Malaysia",
  description:
    "Used cars checked on 210 points and sold at a fixed price across Malaysia. Reserve online, value your trade-in and see your monthly payment.",
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
          <ToastProvider>
            {children}
            <DemoDisclosure />
          </ToastProvider>
        </StaffHistoryBoundary>
      </body>
    </html>
  );
}
