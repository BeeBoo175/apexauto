import React from "react";

export interface TabItem {
  id: string;
  label: string;
  count?: number;
}

export interface TabsProps {
  items: TabItem[];
  activeId: string;
  onChange: (id: string) => void;
  className?: string;
}

export const Tabs: React.FC<TabsProps> = ({ items, activeId, onChange, className = "" }) => {
  return (
    <div className={`flex border-b border-line gap-2 ${className}`.trim()}>
      {items.map((item) => {
        const isActive = item.id === activeId;
        return (
          <button
            key={item.id}
            type="button"
            onClick={() => onChange(item.id)}
            className={`px-4 py-2.5 text-xs uppercase font-bold tracking-wider transition-colors cursor-pointer border-b-2 -mb-0.5 ${
              isActive
                ? "border-signal text-signal bg-paper"
                : "border-transparent text-muted hover:text-graphite"
            }`}
          >
            {item.label}
            {item.count !== undefined && (
              <span className="ml-1.5 px-1.5 py-0.5 rounded-xs bg-canvas text-[10px] text-muted font-mono">
                {item.count}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
};
