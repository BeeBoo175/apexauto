import type { Metadata } from "next";
import type { ReactNode } from "react";
import "./view-transitions.css";

export const metadata: Metadata = {
  title: "Staff workspace | ApexAuto",
  robots: { index: false, follow: false },
};

export default function DashboardLayout({ children }: { children: ReactNode }) {
  return <main className="flex-1">{children}</main>;
}
