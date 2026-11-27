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

export type CheckState = "Met" | "NotMet" | "Borderline" | "NotPublished" | "StudentPending";

export interface Check {
  name: string;
  state: CheckState;
  passed: boolean | null;
  detail: string;
  sourceUrl: string | null;
}

export type Tag =
  | "Eligible" | "LikelyEligible" | "Reach" | "NotEligible" | "GoodFit"
  | "Affordable" | "Stretch" | "OverBudget" | "StaleData" | "Estimate" | "FeeYearUnconfirmed";

export type Priority = "Balanced" | "BestFit" | "LowestCost";

export interface ScoreBreakdown {
  fit: number;
  affordability: number;
  eligibility: number;
  total: number;
  weights: { fit: number; affordability: number; eligibility: number };
}

export interface Recommendation {
  course: Course;
  eligibility: { status: "Eligible" | "LikelyEligible" | "Borderline" | "NotEligible"; checks: Check[]; hasUnknowns: boolean };
  cost: { tuition: number; living: number; oneOff: number; total: number };
  affordability: "Affordable" | "Stretch" | "OverBudget";
  fitScore: number;
  score: number;
  breakdown: ScoreBreakdown;
  tags: Tag[];
  reasons: string[];
}

export interface RecommendationResponse {
  items: Recommendation[];
  fitAvailable: boolean;
  catalogLoadedAt: string;
  targetIntake: string;
  priority: Priority;
}

export interface ExchangeRate {
  currency: string;
  perEur: number;
  date: string;
  source: string;
  sourceUrl: string;
}

export interface Verification {
  usedTools: boolean;
  verified: string[];
  unverified: string[];
  grounded: boolean;
}

export interface ChatTurn {
  role: "user" | "assistant";
  content: string;
  verification?: Verification;
}
