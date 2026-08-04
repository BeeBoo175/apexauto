"use client";

import { createContext, useContext } from "react";
import {
  performanceHref,
  performanceLocation,
  performanceWorkspace,
  type PerformanceParameters,
} from "@/lib/performance/performance-location";
import { useStaffAddress, type AddressMode } from "./use-staff-address";

type Target = NonNullable<ReturnType<typeof performanceLocation>>;
type State = { parameters: PerformanceParameters };

// What the open Performance page writes to the address: the parameters it shows, patched one change at a time.
export const PerformanceAddressContext = createContext<{
  parameters: PerformanceParameters;
  patch: (values: PerformanceParameters, mode?: AddressMode) => void;
}>({ parameters: {}, patch: () => undefined });

export const usePerformanceAddressContext = () => useContext(PerformanceAddressContext);

// Owns the Performance address. An applied change pushes; Back and Forward hand the destination to
// `synchronize`, which reopens the page for it through the entry's own reads.
export function usePerformanceAddress(
  parameters: PerformanceParameters,
  synchronize: (target: Target) => void,
) {
  const address = useStaffAddress<State, Target>({
    workspace: performanceWorkspace,
    initial: { parameters },
    href: (state) => performanceHref(state.parameters),
    locate: performanceLocation,
    synchronize: (target) => {
      address.update({ parameters: target.parameters }, false);
      synchronize(target);
    },
  });

  function patch(values: PerformanceParameters, mode: AddressMode = "push") {
    const next = { ...address.confirmed.current.parameters };
    for (const [key, value] of Object.entries(values)) {
      if (value) {
        next[key] = value;
      } else {
        delete next[key];
      }
    }
    address.update({ parameters: next }, mode);
  }

  return { patch, confirmed: address.confirmed };
}
