import { gradePercent, type FormState } from "./components/ProfileForm";
import type { StudentProfile } from "./types";

export const INITIAL_FORM: FormState = {
  gradeMode: "percent",
  grade: 70,
  background: "computer-science",
  englishTest: "none",
  englishScore: 7,
  workYears: 0,
  budgetLakh: 35,
  inrPerEur: 100,
  interests: ["machine learning"],
};

export function toProfile(f: FormState): StudentProfile {
  return {
    gradePercent: gradePercent(f),
    background: f.background,
    english: f.englishTest === "none" ? null : { test: f.englishTest, score: f.englishScore },
    workYears: f.workYears,
    budgetEur: Math.max(1, Math.round((f.budgetLakh * 100000) / f.inrPerEur)),
    interests: f.interests,
  };
}

/** Rebuilds the form from a saved profile. The budget is stored in euro, so it converts back at today's rate. */
export function fromProfile(p: StudentProfile, inrPerEur: number): FormState {
  return {
    gradeMode: "percent",
    grade: p.gradePercent,
    background: p.background,
    englishTest: p.english?.test ?? "none",
    englishScore: p.english?.score ?? 7,
    workYears: p.workYears,
    budgetLakh: Math.round(((p.budgetEur * inrPerEur) / 100000) * 10) / 10,
    inrPerEur,
    interests: p.interests ?? [],
  };
}
