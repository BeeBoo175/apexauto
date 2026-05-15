"use client";

import { createContext } from "react";
import type { InteractionBlock, InteractionGuard } from "@/lib/interaction-guard";

export const StaffNavigationLockContext = createContext<{
  setLocked: (owner: symbol, locked: boolean | InteractionBlock | null) => void;
  guard: InteractionGuard;
} | null>(null);
