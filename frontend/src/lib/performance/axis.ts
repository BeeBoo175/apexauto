interface DaysScale {
  // Multiply days by this to get the plotted number.
  factor: number;
  unit: "hours" | "days";
  format: (plotted: number) => string;
}

// Days read badly when everything is under one: an axis topping out at 0.4 days says little. When the largest figure is
// under a day the axis plots hours instead, and the unit is part of every tick.
export function daysScale(values: number[]): DaysScale {
  const maximum = Math.max(0, ...values);
  if (maximum < 1) {
    return { factor: 24, unit: "hours", format: (plotted) => `${Number(plotted.toFixed(1))} h` };
  }
  return { factor: 1, unit: "days", format: (plotted) => `${Number(plotted.toFixed(1))} d` };
}
