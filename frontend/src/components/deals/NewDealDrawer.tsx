"use client";

import { useEffect, useState } from "react";
import { StaffWorkspaceFooter } from "@/components/staff/StaffWorkspaceFooter";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { Input } from "@/components/ui/Input";
import { LoadingProgress } from "@/components/ui/LoadingProgress";
import { useToast } from "@/components/ui/Toast";
import { useSettingsSave } from "@/hooks/use-settings-save";
import { privateApiFetch } from "@/lib/api-browser";
import { formatCurrency } from "@/lib/currency-format";
import { parseDeal, parseEligibleVehicles, type EligibleVehicle } from "@/lib/deals/deals";
import workshop from "@/components/inspection/Workshop.module.css";
import workbench from "@/components/staff/StaffWorkspace.module.css";
import forms from "@/components/settings/SettingsForms.module.css";

// A deal starts from an eligible vehicle in the agent's branch; its hold, if any, supplies the customer.
export function NewDealDrawer({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: (id: string) => void;
}) {
  const toast = useToast();
  const saver = useSettingsSave(parseDeal);
  // One identity per open drawer lets a lost create response be retried without a duplicate deal.
  const [id] = useState(() => crypto.randomUUID());
  const [search, setSearch] = useState("");
  const [vehicles, setVehicles] = useState<EligibleVehicle[] | null>(null);
  const [lookupBusy, setLookupBusy] = useState(true);
  const [lookupFailed, setLookupFailed] = useState(false);
  const [chosen, setChosen] = useState<EligibleVehicle | null>(null);
  const [email, setEmail] = useState("");

  async function lookup(term: string) {
    setLookupBusy(true);
    setLookupFailed(false);
    try {
      const query = term.trim() ? `?search=${encodeURIComponent(term.trim())}` : "";
      const result = await privateApiFetch(
        `/api/staff/deals/vehicles${query}`,
        parseEligibleVehicles,
      );
      if (result.status === 200 && result.data) {
        setVehicles(result.data);
      } else {
        setLookupFailed(true);
      }
    } catch {
      setLookupFailed(true);
    } finally {
      setLookupBusy(false);
    }
  }

  useEffect(() => {
    // Opening the drawer lists the branch's eligible vehicles without a search.
    const attempt = new AbortController();
    privateApiFetch("/api/staff/deals/vehicles", parseEligibleVehicles, { signal: attempt.signal })
      .then((result) => {
        if (result.status === 200 && result.data) {
          setVehicles(result.data);
        } else {
          setLookupFailed(true);
        }
      })
      .catch(() => {
        if (!attempt.signal.aborted) {
          setLookupFailed(true);
        }
      })
      .finally(() => {
        if (!attempt.signal.aborted) {
          setLookupBusy(false);
        }
      });
    return () => attempt.abort();
  }, []);

  const needsEmail = chosen !== null && !chosen.hasHold;
  const ready = chosen !== null && (chosen.hasHold || email.trim().includes("@"));

  async function create() {
    if (!chosen || !ready) {
      return;
    }
    const created = await saver.save("/api/staff/deals", "POST", {
      id,
      vehicleId: chosen.vehicleId,
      reservationId: chosen.hasHold ? chosen.reservationId : null,
      customerEmail: chosen.hasHold ? null : email.trim(),
    });
    if (created) {
      toast.success("Deal draft created.");
      onCreated(created.id);
    }
  }

  const notice = saver.notice;
  let message = notice?.message ?? "";
  if (notice?.status === 404) {
    message =
      "No confirmed customer account uses that email, or the vehicle is no longer available.";
  } else if (notice?.status === 409) {
    message =
      "This vehicle already has an open deal or another customer's hold, or the details differ from an earlier attempt.";
  }

  return (
    <>
      <LoadingProgress label="Looking up vehicles" active={lookupBusy} />
      <LoadingProgress label="Creating deal" active={saver.busy} />
      <Dialog
        open
        variant="drawer"
        density="compact"
        layout="workspace"
        title="New deal"
        description="Choose a vehicle in your branch. A held vehicle uses its customer."
        onClose={() => !saver.busy && onClose()}
        closeDisabled={saver.busy}
      >
        <form
          className={workshop.workspace}
          noValidate
          onSubmit={(event) => {
            event.preventDefault();
            void create();
          }}
        >
          <div className={workshop.body}>
            <div className={forms.fields}>
              <section className={forms.section} aria-label="Vehicle">
                <h3>Vehicle</h3>
                <Input
                  label="Search registration"
                  density="compact"
                  value={search}
                  disabled={saver.busy}
                  onChange={(event) => setSearch(event.target.value)}
                  onKeyDown={(event) => {
                    if (event.key === "Enter") {
                      event.preventDefault();
                      void lookup(search);
                    }
                  }}
                />
                <div>
                  <Button
                    type="button"
                    size="compact"
                    variant="secondary"
                    disabled={lookupBusy || saver.busy}
                    onClick={() => void lookup(search)}
                  >
                    Search vehicles
                  </Button>
                </div>
                {lookupFailed && (
                  <p className={forms.error}>Vehicles could not be loaded. Retry the search.</p>
                )}
                {vehicles && vehicles.length === 0 && (
                  <p className={forms.hint}>
                    No eligible vehicles match. Vehicles with an open deal are not listed.
                  </p>
                )}
                <div role="radiogroup" aria-label="Eligible vehicles" className={workbench.stack}>
                  {vehicles?.map((vehicle) => (
                    <label key={vehicle.vehicleId} className={forms.check}>
                      <input
                        type="radio"
                        name="vehicle"
                        checked={chosen?.vehicleId === vehicle.vehicleId}
                        disabled={saver.busy}
                        onChange={() => setChosen(vehicle)}
                      />
                      <span>
                        <strong>{vehicle.registration}</strong> {vehicle.label} ·{" "}
                        {formatCurrency(vehicle.price)}
                        {vehicle.hasHold && ` · Held by ${vehicle.holdCustomer ?? "a customer"}`}
                      </span>
                    </label>
                  ))}
                </div>
              </section>
              {needsEmail && (
                <section className={forms.section} aria-label="Customer">
                  <h3>Customer</h3>
                  <Input
                    label="Customer account email"
                    density="compact"
                    type="email"
                    value={email}
                    disabled={saver.busy}
                    onChange={(event) => setEmail(event.target.value)}
                  />
                  <p className={forms.hint}>The customer must already have a confirmed account.</p>
                </section>
              )}
              {notice && (
                <div className={forms.notice} role="alert">
                  <p>{message}</p>
                </div>
              )}
            </div>
          </div>
          <StaffWorkspaceFooter
            title={chosen ? chosen.registration : "No vehicle chosen"}
            description="The draft opens next so you can add a warranty, discount or trade-in."
          >
            <Button type="submit" size="compact" disabled={saver.busy || !ready}>
              Create draft
            </Button>
          </StaffWorkspaceFooter>
        </form>
      </Dialog>
    </>
  );
}
