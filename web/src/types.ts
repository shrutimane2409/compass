export type EnglishTest = "Ielts" | "Pte" | "Toefl";

export interface StudentProfile {
  gradePercent: number;
  background: string;
  english: { test: EnglishTest; score: number } | null;
  workYears: number;
  budgetEur: number;
  interests: string[];
}

export interface Course {
  id: number;
  university: string;
  city: string;
  title: string;
  field: string;
  durationMonths: number;
  feeNonEuEur: number;
  sourceUrl: string;
  verifiedAt: string;
  isEstimate: boolean;
  feeAcademicYear: string | null;
  englishSourceUrl: string | null;
  entrySourceUrl: string | null;
  gradeSourceUrl: string | null;
}

export interface Check {
  name: string;
  passed: boolean | null;
  detail: string;
}

export type Tag =
  | "Eligible" | "Reach" | "NotEligible" | "GoodFit"
  | "Affordable" | "Stretch" | "OverBudget" | "StaleData" | "Estimate";

export interface Recommendation {
  course: Course;
  eligibility: { status: "Eligible" | "Borderline" | "NotEligible"; checks: Check[]; hasUnknowns: boolean };
  cost: { tuition: number; living: number; oneOff: number; total: number };
  affordability: "Affordable" | "Stretch" | "OverBudget";
  fitScore: number;
  score: number;
  tags: Tag[];
  reasons: string[];
}

export interface RecommendationResponse {
  items: Recommendation[];
  fitAvailable: boolean;
  catalogLoadedAt: string;
}

export interface ChatTurn {
  role: "user" | "assistant";
  content: string;
}
