import fs from "node:fs";
import path from "node:path";
import { createRequire } from "node:module";
import ts from "typescript";

const resolveModule = createRequire(path.join(process.cwd(), "package.json"));

// Bundle the actual local React components; no app, login, backend or business fixture is started.
export function fixtureScript(mode: "dialog" | "settings" | "promo" = "dialog") {
  const entry = { dialog: "App", settings: "SettingsHarness", promo: "PromoHarness" }[mode];
  const modules: string[] = [];
  const identities = new Map<string, number>();
  const root = process.cwd();
  function bundle(file: string): number {
    const resolved = resolveModule.resolve(file);
    const existing = identities.get(resolved);
    if (existing !== undefined) {
      return existing;
    }
    const id = modules.length;
    identities.set(resolved, id);
    modules.push("");
    let source = resolved.endsWith(".css")
      ? "module.exports = new Proxy({}, { get: (_, key) => String(key) });"
      : fs.readFileSync(resolved, "utf8");
    if (/\.tsx?$/.test(resolved)) {
      source = ts.transpileModule(source, {
        compilerOptions: {
          target: ts.ScriptTarget.ES2022,
          module: ts.ModuleKind.CommonJS,
          jsx: ts.JsxEmit.ReactJSX,
          esModuleInterop: true,
        },
      }).outputText;
    }
    source = source.replace(/require\(["']([^"']+)["']\)/g, (_, name: string) => {
      let target = name;
      if (name.startsWith("@/")) {
        target = path.join(root, "src", name.slice(2));
      }
      if (name.startsWith(".")) {
        target = path.resolve(path.dirname(resolved), name);
      }
      if (path.isAbsolute(target) && !fs.existsSync(target)) {
        target =
          [".tsx", ".ts", ".js"]
            .map((extension) => target + extension)
            .find((candidate) => fs.existsSync(candidate)) ?? target;
      }
      return `load(${bundle(resolveModule.resolve(target, { paths: [path.dirname(resolved)] }))})`;
    });
    modules[id] = `function(module,exports,load){${source}\n}`;
    return id;
  }
  const react = bundle(resolveModule.resolve("react"));
  const dom = bundle(resolveModule.resolve("react-dom/client"));
  const dialog = bundle(path.join(root, "src/components/ui/Dialog.tsx"));
  const hook = bundle(path.join(root, "src/hooks/use-edit-guard.ts"));
  const context = bundle(path.join(root, "src/components/staff/StaffNavigationLockContext.tsx"));
  const registry = bundle(path.join(root, "src/lib/interaction-guard.ts"));
  const toast = bundle(path.join(root, "src/components/ui/Toast.tsx"));
  const feedback = bundle(path.join(root, "src/components/ui/EditGuardFeedback.tsx"));
  const settings = bundle(path.join(root, "src/components/settings/SettingsForm.tsx"));
  const guardedAction = bundle(path.join(root, "src/hooks/use-guarded-action.ts"));
  const promo = bundle(path.join(root, "src/components/settings/PromoForm.tsx"));
  const saver = bundle(path.join(root, "src/hooks/use-settings-save.ts"));
  const settingsPolicy = bundle(path.join(root, "src/lib/settings/settings.ts"));
  return `(() => {
    const process = { env: { NODE_ENV: "development" } };
    const modules = [${modules.join(",")}];
    const cache = {};

    function load(id) {
      if (cache[id]) {
        return cache[id].exports;
      }
      const module = { exports: {} };
      cache[id] = module;
      modules[id](module, module.exports, load);
      return module.exports;
    }

    const React = load(${react});
    const { createRoot } = load(${dom});
    const { Dialog } = load(${dialog});
    const { useEditGuard } = load(${hook});
    const { StaffNavigationLockContext } = load(${context});
    const { createInteractionGuard } = load(${registry});
    const { ToastProvider } = load(${toast});
    const { EditGuardFeedback } = load(${feedback});
    const { SettingsForm } = load(${settings});
    const { useGuardedAction } = load(${guardedAction});
    const { PromoForm } = load(${promo});
    const { useSettingsSave } = load(${saver});
    const { parsePromo } = load(${settingsPolicy});
    const h = React.createElement;
    const navigationGuard = createInteractionGuard();
    const navigation = {
      guard: navigationGuard,
      setLocked: (owner, locked) => navigationGuard.setLocked(owner, locked),
    };
    window.navigationLocked = () => navigationGuard.isLocked();
    const operationOwner = Symbol("background save");
    window.operationState = (kind) => navigation.setLocked(operationOwner, kind ? { kind, label: "Saving terms" } : false);

    function BlockingReason() {
      const { message } = useGuardedAction();
      return h("output", { "aria-label": "Blocking reason" }, message ?? "");
    }

    function SettingsHarness() {
      const [settings, setSettings] = React.useState({
        processingFee: 500,
        sstRate: 0.08,
        maxAgentDiscountAmount: 500,
        reservationHoldHours: 48,
        updatedAtUtc: "2026-11-01T02:00:00Z",
        version: 1,
        bounds: { maximumFee: 100000, maximumSstRate: 1, maximumDiscount: 100000, minimumHoldHours: 1, maximumHoldHours: 168 },
      });
      const [draft, setDraft] = React.useState(null);
      return h(ToastProvider, null,
        h(StaffNavigationLockContext.Provider, { value: navigation },
          h(EditGuardFeedback, { guard: navigationGuard, surface: Dialog }),
          h(SettingsForm, { settings, draft, busy: false, onDraft: setDraft, onSaved: setSettings, onLocked: () => {} }),
        ),
      );
    }

    function PromoHarness() {
      const saver = useSettingsSave(parsePromo);
      const [done, setDone] = React.useState(false);
      const finish = () => setDone(true);
      let content = h("output", null, "Promo checked");
      if (!done) {
        content = h(PromoForm, { base: null, latest: null, saver, busy: false,
          onSaved: finish, onReload: () => {}, onUseLatest: () => {}, onCancel: finish });
      }
      return h(ToastProvider, null,
        h(StaffNavigationLockContext.Provider, { value: navigation },
          h(EditGuardFeedback, { guard: navigationGuard, surface: Dialog }), content,
        ),
      );
    }

    function Fields({ name }) {
      const [value, setValue] = React.useState("");
      const lock = useEditGuard({ dirty: Boolean(value), label: name, discard: () => setValue("") });
      return h("input", {
        "aria-label": name,
        value,
        onChange: (event) => {
          lock(true);
          setValue(event.target.value);
        },
        onKeyDown: (event) => {
          if (event.key === "Escape") {
            window.localEscape = true;
          }
        },
      });
    }

    function App() {
      const [open, setOpen] = React.useState(true);
      const [inner, setInner] = React.useState(false);
      const [locked, setLocked] = React.useState(false);
      window.explicitLock = setLocked;

      const innerDialog = h(Dialog, {
        open: inner,
        title: "Inner editor",
        onClose: () => setInner(false),
      }, h(Fields, { name: "Inner draft" }));

      const outerDialog = h(Dialog, {
        open,
        title: "Outer editor",
        closeDisabled: locked,
        onClose: () => setOpen(false),
      },
        h(Fields, { name: "First draft" }),
        h(Fields, { name: "Second draft" }),
        h("button", { onClick: () => setInner(true) }, "Open reference"),
        innerDialog,
      );

      return h(ToastProvider, null,
        h(StaffNavigationLockContext.Provider, { value: navigation },
          h(EditGuardFeedback, { guard: navigationGuard, surface: Dialog }),
          h(BlockingReason),
          outerDialog,
        ),
      );
    }

    createRoot(document.getElementById("root")).render(h(React.StrictMode, null, h(${entry})));
  })();`;
}
