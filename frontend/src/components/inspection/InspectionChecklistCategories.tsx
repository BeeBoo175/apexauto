import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { ScrollStrip } from "@/components/ui/ScrollStrip";
import { label } from "@/lib/inspection/contracts";
import styles from "./Workshop.module.css";

export function InspectionChecklistCategories({
  categories,
  matches,
  category,
  disabled,
  onChange,
}: {
  categories: readonly string[];
  matches: readonly { category: string }[];
  category: string;
  disabled: boolean;
  onChange: (category: string) => void;
}) {
  return (
    <nav className={styles.categoryNavigation} aria-label="Inspection categories">
      <ScrollStrip label="Inspection categories" className={styles.categoryChipsStrip}>
        {categories.map((name) => {
          const count = matches.filter((point) => point.category === name).length;
          const selected = name === category;
          return (
            <Button
              key={name}
              size="compact"
              variant={selected ? "secondary" : "ghost"}
              className={styles.categoryChip}
              aria-pressed={selected}
              disabled={disabled}
              onClick={() => onChange(name)}
            >
              <span>{label(name)}</span>
              <Badge variant="count" data-selected={selected}>
                {count}
              </Badge>
            </Button>
          );
        })}
      </ScrollStrip>
    </nav>
  );
}
