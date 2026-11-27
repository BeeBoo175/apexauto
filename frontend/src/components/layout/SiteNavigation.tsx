"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Button } from "@/components/ui/Button";
import { Icon } from "@/components/ui/Icon";
import {
  matchesSitePath,
  SITE_NAVIGATION,
  type SiteNavigationEntry,
  type SiteNavigationGroup,
  type SiteNavigationLink,
} from "./site-navigation-links";
import styles from "./SiteChrome.module.css";

interface SiteNavigationProps {
  // True while a static page is still asking for the account links.
  accountPending?: boolean;
  showAccount: boolean;
  staffPortalHref: string | null;
}

function pathOf(href: string) {
  return href.split("#")[0];
}

function groupIsActive(pathname: string, group: SiteNavigationGroup) {
  return group.links.some((link) => matchesSitePath(pathname, pathOf(link.href)));
}

export function SiteNavigation({
  accountPending = false,
  showAccount,
  staffPortalHref,
}: SiteNavigationProps) {
  const pathname = usePathname();
  const [menuOpen, setMenuOpen] = useState(false);
  const [openGroup, setOpenGroup] = useState<string | null>(null);
  // A menu opened by a click stays open until closed; one opened by hover closes when the pointer leaves.
  const [pinned, setPinned] = useState(false);
  const toggleRef = useRef<HTMLButtonElement | null>(null);
  const headerNavRef = useRef<HTMLDivElement | null>(null);
  const [lastPathname, setLastPathname] = useState(pathname);

  // A page change closes every open menu.
  if (pathname !== lastPathname) {
    setLastPathname(pathname);
    setMenuOpen(false);
    setOpenGroup(null);
    setPinned(false);
  }

  useEffect(() => {
    if (!menuOpen && !openGroup) {
      return;
    }

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") {
        setMenuOpen(false);
        setOpenGroup(null);
        setPinned(false);
        toggleRef.current?.focus();
      }
    }

    function onPointerDown(event: PointerEvent) {
      if (headerNavRef.current && !headerNavRef.current.contains(event.target as Node)) {
        setMenuOpen(false);
        setOpenGroup(null);
        setPinned(false);
      }
    }

    window.addEventListener("keydown", onKeyDown);
    window.addEventListener("pointerdown", onPointerDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("pointerdown", onPointerDown);
    };
  }, [menuOpen, openGroup]);

  const accountHref = staffPortalHref ? "/dashboard/profile" : "/account";

  function closeMenus() {
    setMenuOpen(false);
    setOpenGroup(null);
    setPinned(false);
  }

  function renderLink(link: SiteNavigationLink) {
    return (
      <Link
        key={link.href}
        href={link.href}
        prefetch={link.prefetch}
        aria-current={matchesSitePath(pathname, pathOf(link.href)) ? "page" : undefined}
        onClick={closeMenus}
      >
        {link.label}
      </Link>
    );
  }

  function renderGroup(group: SiteNavigationGroup) {
    const open = openGroup === group.id;
    const panelId = `site-menu-${group.id}`;

    return (
      <div
        key={group.id}
        className={styles.navItem}
        onPointerEnter={(event) => {
          if (event.pointerType === "mouse" && !pinned) {
            setOpenGroup(group.id);
          }
        }}
        onPointerLeave={(event) => {
          if (event.pointerType === "mouse" && !pinned) {
            setOpenGroup(null);
          }
        }}
      >
        <button
          type="button"
          className={styles.navTrigger}
          aria-expanded={open}
          aria-controls={panelId}
          data-active={groupIsActive(pathname, group) || undefined}
          onClick={() => {
            // A click on an open menu closes it; a click on any other state pins it open.
            const close = open && pinned;
            setOpenGroup(close ? null : group.id);
            setPinned(!close);
          }}
        >
          <span>{group.label}</span>
          <span className={styles.navCaret}>
            <Icon name="down" />
          </span>
        </button>
        {open && (
          <div id={panelId} className={styles.dropdown}>
            {group.links.map(renderLink)}
          </div>
        )}
      </div>
    );
  }

  function renderEntry(entry: SiteNavigationEntry) {
    return entry.kind === "group" ? renderGroup(entry.group) : renderLink(entry.link);
  }

  function renderAccountLinks() {
    return (
      <>
        {accountPending && (
          // Holds the Account link’s room so the navigation does not move when the answer arrives.
          <a className={styles.accountPlaceholder} aria-hidden="true">
            Account
          </a>
        )}
        {showAccount && (
          <a
            href={accountHref}
            aria-current={matchesSitePath(pathname, "/account") ? "page" : undefined}
            onClick={closeMenus}
          >
            Account
          </a>
        )}
        {staffPortalHref && (
          <Link
            href={staffPortalHref}
            prefetch={false}
            aria-current={matchesSitePath(pathname, "/dashboard") ? "page" : undefined}
            onClick={closeMenus}
          >
            Staff portal
          </Link>
        )}
      </>
    );
  }

  const groups = SITE_NAVIGATION.flatMap((entry) => (entry.kind === "group" ? [entry.group] : []));
  const looseLinks = SITE_NAVIGATION.flatMap((entry) =>
    entry.kind === "link" ? [entry.link] : [],
  );

  return (
    <div ref={headerNavRef} className={styles.navContainer}>
      <nav className={styles.navigation} aria-label="Main navigation">
        {SITE_NAVIGATION.map(renderEntry)}
        {renderAccountLinks()}
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
          <span className={styles.menuLabel}>{menuOpen ? "Close" : "Menu"}</span>
        </Button>
      </div>

      {menuOpen && (
        <nav
          id="site-mobile-navigation"
          className={styles.mobileMenu}
          aria-label="Mobile navigation"
        >
          {groups.map((group) => (
            <div key={group.id} className={styles.mobileGroup}>
              <p className={styles.mobileGroupLabel}>{group.label}</p>
              {group.links.map(renderLink)}
            </div>
          ))}
          <div className={styles.mobileGroup}>
            {looseLinks.map(renderLink)}
            {renderAccountLinks()}
          </div>
        </nav>
      )}
    </div>
  );
}
