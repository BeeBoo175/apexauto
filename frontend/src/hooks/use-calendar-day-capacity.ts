"use client";

import { useLayoutEffect, useRef, useState } from "react";

const chipGap = 3;
const moreLabelHeight = 20;

// How many appointment chips a month cell can hold without cutting one off: the cell's height less its date row, in
// units of the chip height actually rendered. A cell that cannot hold every chip also keeps room for its "+N more".
export function useCalendarDayCapacity(count: number, maximum: number) {
  const day = useRef<HTMLDivElement>(null);
  const [capacity, setCapacity] = useState(maximum);

  useLayoutEffect(() => {
    const element = day.current;
    if (!element) {
      return;
    }
    function measure() {
      const cell = day.current;
      const chip = cell?.querySelector<HTMLElement>("[data-calendar-event]")?.offsetHeight ?? 0;
      if (!cell || chip === 0) {
        setCapacity(maximum);
        return;
      }
      const top = cell.querySelector<HTMLElement>("[data-day-top]")?.offsetHeight ?? 0;
      const room = cell.clientHeight - top;
      const fitsAll = Math.floor((room + chipGap) / (chip + chipGap));
      const fitsWithMore = Math.floor((room - moreLabelHeight + chipGap) / (chip + chipGap));
      const next = count <= maximum && fitsAll >= count ? count : fitsWithMore;
      setCapacity(Math.min(maximum, Math.max(1, next)));
    }
    measure();
    const observer = new ResizeObserver(measure);
    observer.observe(element);
    return () => observer.disconnect();
  }, [count, maximum]);

  return { day, capacity };
}
