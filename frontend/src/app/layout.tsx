import type { Metadata } from "next";
import "./globals.css";
import { SiteHeader } from "@/components/layout/SiteHeader";
import { SiteFooter } from "@/components/layout/SiteFooter";
import { ToastProvider } from "@/components/ui/Toast";
import { IconSprite } from "@/components/ui/IconSprite";

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
      <body className="min-h-screen flex flex-col bg-canvas text-graphite">
        <IconSprite />
        <ToastProvider>
          <SiteHeader />
          <main className="flex-1">{children}</main>

          <SiteFooter />
        </ToastProvider>
      </body>
    </html>
  );
}
