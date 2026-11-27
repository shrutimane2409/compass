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
  applicationDeadline: string | null;
  rankings: UniversityRank[];
}

export interface UniversityRank {
  source: "QS" | "THE";
  edition: number;
  rankText: string;
  rankLow: number;
  sourceUrl: string;
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

export type Priority = "Balanced" | "BestFit" | "LowestCost" | "Reputation";

export interface ScoreBreakdown {
  fit: number;
  affordability: number;
  eligibility: number;
  total: number;
  reputation: number;
  weights: { fit: number; affordability: number; eligibility: number; reputation: number };
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

export interface Scholarship {
  id: number;
  slug: string;
  name: string;
  provider: string;
  university: string | null;
  amountText: string;
  maxValueEur: number | null;
  automatic: boolean;
  competitive: boolean;
  requiresOffer: boolean;
  howToApply: string;
  deadlineText: string;
  sourceUrl: string;
  verifiedAt: string;
}

export interface ScholarshipMatch {
  scholarship: Scholarship;
  fit: "Likely" | "Possible" | "NotApplicable";
  courseIds: number[];
  reasons: string[];
}

export interface User {
  id: string;
  email: string;
  name: string | null;
  pictureUrl: string | null;
}

export type AppStatus = "researching" | "preparing" | "applied" | "offer" | "accepted" | "rejected";

export interface Application {
  courseId: number;
  status: AppStatus;
  documents: Record<string, boolean>;
  notes: string | null;
  updatedAt: string;
}

export interface SavedCourse {
  courseId: number;
  note: string | null;
  createdAt: string;
}

export interface Me {
  user: User;
  profile: StudentProfile | null;
  priority: Priority;
  saved: SavedCourse[];
  applications: Application[];
  checklistDone: string[];
}
