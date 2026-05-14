"use client";

import { createContext, type RefObject } from "react";
import type { InteractionBlock, InteractionGuard } from "@/lib/interaction-guard";

export const StaffNavigationLockContext = createContext<{
  lockedRef: RefObject<boolean>;
  setLocked: (locked: boolean | InteractionBlock | null, owner?: symbol) => void;
  guard: InteractionGuard;
} | null>(null);
