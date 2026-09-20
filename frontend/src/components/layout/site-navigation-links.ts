export interface SiteNavigationLink {
  href: string;
  label: string;
  prefetch: boolean;
}

export interface SiteNavigationGroup {
  id: string;
  label: string;
  links: readonly SiteNavigationLink[];
}

export type SiteNavigationEntry =
  { kind: "link"; link: SiteNavigationLink } | { kind: "group"; group: SiteNavigationGroup };

// Top-level entries in header order. Pages that explain how things work sit apart from the pages where
// the visitor acts, so each dropdown holds one kind of page.
export const SITE_NAVIGATION: readonly SiteNavigationEntry[] = [
  {
    kind: "group",
    group: {
      id: "buy",
      label: "Buy a car",
      links: [
        { href: "/vehicles", label: "Browse cars", prefetch: false },
        { href: "/warranty", label: "ApexShield warranty", prefetch: true },
      ],
    },
  },
  { kind: "link", link: { href: "/sell", label: "Sell my car", prefetch: false } },
  {
    kind: "group",
    group: {
      id: "how",
      label: "How it works",
      links: [
        { href: "/buying-guide", label: "Buying a car", prefetch: true },
        { href: "/selling-guide", label: "Selling a car", prefetch: true },
        { href: "/inspection", label: "210-point inspection", prefetch: true },
      ],
    },
  },
  { kind: "link", link: { href: "/branches", label: "Showrooms", prefetch: true } },
  { kind: "link", link: { href: "/about", label: "About", prefetch: true } },
  {
    kind: "group",
    group: {
      id: "help",
      label: "Help",
      links: [
        { href: "/faq", label: "FAQ", prefetch: true },
        { href: "/disclaimer", label: "Disclaimers and notices", prefetch: true },
      ],
    },
  },
];

export function matchesSitePath(pathname: string, href: string) {
  return pathname === href || pathname.startsWith(`${href}/`);
}
