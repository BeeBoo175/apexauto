import type { ReactNode } from "react";
import { PageMotion } from "@/components/layout/PageMotion";

// A template, unlike a layout, mounts again on every navigation, so each public page gets its short entrance.
export default function PublicTemplate({ children }: { children: ReactNode }) {
  return <PageMotion>{children}</PageMotion>;
}
