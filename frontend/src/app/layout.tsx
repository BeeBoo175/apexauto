import type { Metadata } from "next";
import Link from "next/link";
import "./globals.css";
import { ToastProvider } from "@/components/ui/Toast";

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
        <ToastProvider>
          <header className="border-b border-line bg-graphite text-paper px-6 py-4 flex flex-wrap gap-4 items-center justify-between">
            <div className="flex items-center gap-3">
              <span className="text-xl font-bold tracking-widest uppercase font-display">
                APEX<span className="text-signal">AUTO</span>
              </span>
              <span className="text-[10px] uppercase tracking-widest px-2 py-0.5 border border-graphite-soft text-muted">
                DMS &amp; STOREFRONT
              </span>
            </div>
            <nav className="flex items-center gap-6 text-xs uppercase font-bold tracking-wider">
              <a href="/account" className="hover:text-signal transition-colors">
                Account
              </a>
              <Link href="/vehicles" className="hover:text-signal transition-colors">
                Inventory
              </Link>
              <Link href="/sell" className="hover:text-signal transition-colors">
                Sell Car
              </Link>
              <Link href="/dashboard" className="text-muted hover:text-paper transition-colors">
                Staff Portal
              </Link>
            </nav>
          </header>

          <main className="flex-1">{children}</main>

          <footer className="border-t border-line bg-paper px-6 py-6 text-xs text-muted flex flex-wrap gap-4 items-center justify-between">
            <div>&copy; 2026 ApexAuto Sdn Bhd. All rights reserved.</div>
            <div className="flex gap-4 uppercase font-bold text-[10px] tracking-wider">
              <span>JPJ / Puspakom Conventions</span>
              <span>•</span>
              <span>Kuala Lumpur, Malaysia</span>
            </div>
          </footer>
        </ToastProvider>
      </body>
    </html>
  );
}
