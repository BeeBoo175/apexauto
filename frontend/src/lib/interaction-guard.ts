export interface InteractionBlock {
  kind: "draft" | "pending" | "uncertain";
  label: string;
  discard?: () => void;
}

interface LeaveIntent {
  action: () => void;
  label: string;
  owners: symbol[];
}

// Each editor owns its draft and operation; releasing one cannot unlock another.
export function createInteractionGuard() {
  const owners = new Map<symbol, InteractionBlock>();
  const listeners = new Set<() => void>();
  let intent: LeaveIntent | null = null;
  let notice: { message: string } | null = null;
  let snapshot = read();

  function read() {
    const blocks = [...owners.values()];
    const hard =
      blocks.find((block) => block.kind === "uncertain") ??
      blocks.find((block) => block.kind === "pending") ??
      blocks.find((block) => !block.discard);
    let message = "";
    if (hard) {
      message = `${hard.label}: wait for the action to finish.`;
      if (hard.kind === "uncertain") {
        message = `${hard.label}: reconcile the unconfirmed action before leaving.`;
      }
      if (hard.kind === "draft") {
        message = `${hard.label}: save or discard your changes in the editor before leaving.`;
      }
    }
    return { locked: blocks.length > 0, blocked: Boolean(hard), message, intent, notice };
  }

  function publish() {
    snapshot = read();
    listeners.forEach((listener) => listener());
  }

  function setLocked(owner: symbol, value: boolean | InteractionBlock | null) {
    const block = value === true ? { kind: "pending" as const, label: "Current action" } : value;
    const previous = owners.get(owner);
    if (!block) {
      if (!owners.delete(owner)) {
        return;
      }
    } else {
      if (
        previous?.kind === block.kind &&
        previous.label === block.label &&
        previous.discard === block.discard
      ) {
        return;
      }
      owners.set(owner, block);
    }
    publish();
  }

  function request(action: () => void, label = "Discard and continue") {
    if (snapshot.blocked) {
      notice = { message: snapshot.message };
      publish();
      return;
    }
    if (!owners.size) {
      action();
      return;
    }
    intent = { action, label, owners: [...owners.keys()] };
    publish();
  }

  function cancel() {
    intent = null;
    publish();
  }

  function confirm() {
    const current = intent;
    if (!current) {
      return;
    }
    if (snapshot.blocked) {
      cancel();
      request(current.action, current.label);
      return;
    }
    if ([...owners.keys()].some((owner) => !current.owners.includes(owner))) {
      request(current.action, current.label);
      return;
    }
    const drafts = [...owners.values()];
    intent = null;
    drafts.forEach((block) => block.discard?.());
    publish();
    current.action();
  }
  function subscribe(listener: () => void) {
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }
  return {
    isLocked: () => snapshot.locked,
    getSnapshot: () => snapshot,
    setLocked,
    subscribe,
    request,
    cancel,
    confirm,
    clearNotice: () => {
      notice = null;
      publish();
    },
  };
}

export type InteractionGuard = ReturnType<typeof createInteractionGuard>;
