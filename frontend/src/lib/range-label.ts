// Applied-filter chip text for numeric range columns; a blank bound reads as open-ended.
export function moneyRangeLabel(minimum = "", maximum = "") {
  return `RM ${minimum || "0"}–${maximum || "any"}`;
}

export function hoursRangeLabel(minimum = "", maximum = "") {
  return `${minimum || "0"}–${maximum || "any"} hours`;
}
