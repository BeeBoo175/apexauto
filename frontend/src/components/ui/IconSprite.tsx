import "server-only";
import styles from "./Icon.module.css";

import { readFileSync } from "node:fs";
import path from "node:path";
import { iconFiles } from "@/lib/icons";

const symbols = Object.entries(iconFiles)
  .map(([name, file]) => {
    const source = readFileSync(path.join(process.cwd(), "public", "icons", file), "utf8");
    return source
      .replace(/<svg\b/, `<symbol id="apex-icon-${name}"`)
      .replace(/<\/svg>/, "</symbol>");
  })
  .join("\n");

export function IconSprite() {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      data-icon-sprite=""
      aria-hidden="true"
      focusable="false"
      width="0"
      height="0"
      className={styles.sprite}
      // Only the fixed, checked-in SVG files above supply this markup.
      dangerouslySetInnerHTML={{ __html: symbols }}
    />
  );
}
