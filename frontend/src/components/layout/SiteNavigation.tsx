"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import styles from "./SiteChrome.module.css";

interface SiteNavigationProps {
  showAccount: boolean;
  showStaffPortal: boolean;
}

const PUBLIC_LINKS = [
  { href: "/vehicles", label: "Buy car", prefetch: false },
  { href: "/sell", label: "Sell car", prefetch: false },
  { href: "/inspection", label: "Inspection", prefetch: true },
  { href: "/branches", label: "Branches", prefetch: true },
  { href: "/buying-guide", label: "Buying guide", prefetch: true },
] as const;

function matchesPath(pathname: string, href: string) {
  if (href === "/vehicles") {
    return pathname === "/vehicles" || pathname.startsWith("/vehicles/");
  }
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function SiteNavigation({ showAccount, showStaffPortal }: SiteNavigationProps) {
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);
  const toggleRef = useRef<HTMLButtonElement | null>(null);
  const headerNavRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (!menuOpen) {
      return;
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setMenuOpen(false);
        toggleRef.current?.focus();
      }
    }

    function onPointerDown(event: PointerEvent) {
      if (headerNavRef.current && !headerNavRef.current.contains(event.target as Node)) {
        setMenuOpen(false);
      }
    }

    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("pointerdown", onPointerDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("pointerdown", onPointerDown);
    };
  }, [menuOpen]);

  const accountHref = showStaffPortal ? "/dashboard/profile" : "/account";

  function closeMenu() {
    setMenuOpen(false);
  }

  function renderLinks(mobile: boolean) {
    return (
      <>
        {PUBLIC_LINKS.map((item) => {
          const active = matchesPath(pathname, item.href);
          return (
            <Link
              key={item.href}
              href={item.href}
              prefetch={item.prefetch}
              aria-current={active ? "page" : undefined}
              onClick={mobile ? closeMenu : undefined}
            >
              {item.label}
            </Link>
          );
        })}
        {showAccount && (
          <a
            href={accountHref}
            aria-current={matchesPath(pathname, "/account") ? "page" : undefined}
            onClick={mobile ? closeMenu : undefined}
          >
            Account
          </a>
        )}
        {showStaffPortal && (
          <a
            href="/dashboard"
            aria-current={matchesPath(pathname, "/dashboard") ? "page" : undefined}
            onClick={mobile ? closeMenu : undefined}
          >
            Staff portal
          </a>
        )}
      </>
    );
  }

  return (
    <div ref={headerNavRef} className={styles.navContainer}>
      <nav className={styles.navigation} aria-label="Main navigation">
        {renderLinks(false)}
      </nav>

      <div className={styles.mobileControls}>
        <Button
          ref={toggleRef}
          type="button"
          variant="shell"
          size="sm"
          className={styles.menuToggle}
          aria-expanded={menuOpen}
          aria-controls="site-mobile-navigation"
          aria-label={menuOpen ? "Close menu" : "Open menu"}
          onClick={() => setMenuOpen((current) => !current)}
        >
          <Icon name={menuOpen ? "close" : "menu"} />
          <span>{menuOpen ? "Close" : "Menu"}</span>
        </Button>
      </div>

      {menuOpen && (
        <nav
          id="site-mobile-navigation"
          className={styles.mobileMenu}
          aria-label="Mobile navigation"
        >
          {renderLinks(true)}
        </nav>
      )}
    </div>
  );
}
