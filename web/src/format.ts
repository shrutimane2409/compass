export const eur = (n: number) =>
  new Intl.NumberFormat("en-IE", { style: "currency", currency: "EUR", maximumFractionDigits: 0 }).format(n);

export const inrLakh = (eurAmount: number, rate: number) =>
  `₹${((eurAmount * rate) / 100000).toLocaleString("en-IN", { maximumFractionDigits: 1 })} lakh`;

export const TAG_LABEL: Record<string, string> = {
  Eligible: "Eligible",
  Reach: "Reach",
  NotEligible: "Not eligible",
  GoodFit: "Good fit",
  Affordable: "Affordable",
  Stretch: "Stretch",
  OverBudget: "Over budget",
  StaleData: "Check dates",
  Estimate: "Includes estimates",
};

export const CGPA_FACTOR = 9.5;
