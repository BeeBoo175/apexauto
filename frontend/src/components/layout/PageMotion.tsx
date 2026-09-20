"use client";

import { useEffect, type ReactNode } from "react";
import styles from "./PageMotion.module.css";

// Sections that already move on their own, or open the page, are left alone.
function movesItself(section: Element) {
  return (
    section.querySelector("h1, [data-reveal], [data-no-motion]") !== null ||
    section.hasAttribute("data-no-motion")
  );
}

// Site-wide motion for the public pages, kept short and small: the page eases in when it opens, and each section's
// content rises a little the first time it scrolls into view. Sections already on screen when the page opens, and
// anything the browser's reduced-motion setting rules out, are left as they are.
export function PageMotion({ children }: { children: ReactNode }) {
  useEffect(() => {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
      return;
    }
    const observers: IntersectionObserver[] = [];
    const sections = document.querySelectorAll("main section");

    sections.forEach((section) => {
      const content = section.firstElementChild;
      if (!content || movesItself(section)) {
        return;
      }
      if (section.getBoundingClientRect().top < window.innerHeight * 0.85) {
        return;
      }
      content.setAttribute("data-sr", "hidden");
      const observer = new IntersectionObserver(
        (entries) => {
          // Entries can arrive in a batch, so any one of them being in view is enough.
          if (entries.some((entry) => entry.isIntersecting)) {
            observer.disconnect();
            content.setAttribute("data-sr", "shown");
          }
        },
        { threshold: 0.08 },
      );
      observer.observe(section);
      observers.push(observer);
    });

    return () => {
      observers.forEach((observer) => observer.disconnect());
      // A section left hidden by leaving the page early must not stay hidden if the page is shown again.
      document.querySelectorAll('main [data-sr="hidden"]').forEach((node) => {
        node.removeAttribute("data-sr");
      });
    };
  }, []);

  return <div className={styles.page}>{children}</div>;
}
