export const eur = (n: number) =>
  new Intl.NumberFormat("en-IE", { style: "currency", currency: "EUR", maximumFractionDigits: 0 }).format(n);

export const inrLakh = (eurAmount: number, rate: number) =>
  `₹${((eurAmount * rate) / 100000).toLocaleString("en-IN", { maximumFractionDigits: 1 })} lakh`;

export const TAG_LABEL: Record<string, string> = {
  Eligible: "Eligible",
  LikelyEligible: "Check requirements",
  Reach: "Reach",
  NotEligible: "Not eligible",
  GoodFit: "Good fit",
  Affordable: "Affordable",
  Stretch: "Stretch",
  OverBudget: "Over budget",
  StaleData: "Check dates",
  Estimate: "Includes estimates",
  FeeYearUnconfirmed: "Fee year unconfirmed",
};

export const TAG_HINT: Record<string, string> = {
  Eligible: "Every requirement is published and you meet all of them.",
  LikelyEligible: "Nothing rules you out, but at least one requirement isn't published or you haven't provided it yet.",
  Reach: "You're close to a minimum, or the university decides case by case.",
  NotEligible: "You don't meet at least one published requirement.",
  FeeYearUnconfirmed: "The fee shown may be for an earlier academic year.",
  Estimate: "Some values are derived (e.g. a range midpoint), not stated on the page.",
  StaleData: "These details haven't been re-checked recently.",
};

export const pct = (n: number) => `${Math.round(n * 100)}%`;

export const CGPA_FACTOR = 9.5;
