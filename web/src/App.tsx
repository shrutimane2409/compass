import { useEffect, useMemo, useRef, useState } from "react";
import { useAccount } from "./account";
import { getCourses, getExchangeRate, getRecommendations } from "./api";
import { Icon, Logo } from "./components/Icons";
import type { FormState } from "./components/ProfileForm";
import { SECTIONS, Sidebar, type Section } from "./components/Sidebar";
import { Applications } from "./pages/Applications";
import { Dashboard } from "./pages/Dashboard";
import { Explore } from "./pages/Explore";
import { Prearrival } from "./pages/Prearrival";
import { Profile } from "./pages/Profile";
import { Saved } from "./pages/Saved";
import { Scholarships } from "./pages/Scholarships";
import { fromProfile, INITIAL_FORM, toProfile } from "./profile";
import type { Course, ExchangeRate, Priority, RecommendationResponse } from "./types";

function readSection(): Section {
  const id = location.hash.replace(/^#\//, "");
  return SECTIONS.some((s) => s.id === id) ? (id as Section) : "dashboard";
}

/** Shared state for every section, kept here so moving between sections never loses a shortlist. */
export interface Shared {
  form: FormState;
  setForm: (f: FormState) => void;
  submitted: boolean;
  submit: () => void;
  data: RecommendationResponse | null;
  loading: boolean;
  error: string;
  priority: Priority;
  setPriority: (p: Priority) => void;
  fx: ExchangeRate | null;
  courses: Map<number, Course>;
}

export default function App() {
  const account = useAccount();
  const [section, setSection] = useState<Section>(readSection);
  const [menuOpen, setMenuOpen] = useState(false);
  const [form, setForm] = useState<FormState>(INITIAL_FORM);
  const [submitted, setSubmitted] = useState(false);
  const [data, setData] = useState<RecommendationResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [priority, setPriority] = useState<Priority>("Balanced");
  const [fx, setFx] = useState<ExchangeRate | null>(null);
  const [courses, setCourses] = useState<Map<number, Course>>(new Map());
  const inflight = useRef<AbortController | null>(null);
  const restored = useRef(false);

  useEffect(() => {
    const onHash = () => { setSection(readSection()); window.scrollTo(0, 0); };
    window.addEventListener("hashchange", onHash);
    return () => window.removeEventListener("hashchange", onHash);
  }, []);

  // Official ECB reference rate for INR, so the rupee budget converts at a real, dated rate.
  useEffect(() => {
    getExchangeRate("INR").then((rate) => {
      if (!rate) return;
      setFx(rate);
      setForm((f) => (f.inrPerEur === INITIAL_FORM.inrPerEur ? { ...f, inrPerEur: Math.round(rate.perEur * 100) / 100 } : f));
    });
    getCourses().then((list) => setCourses(new Map(list.map((c) => [c.id, c])))).catch(() => {});
  }, []);

  // A returning student's saved profile fills the form and produces their shortlist straight away.
  useEffect(() => {
    if (restored.current || !account.me?.profile) return;
    restored.current = true;
    setForm((f) => fromProfile(account.me!.profile!, f.inrPerEur));
    setPriority(account.me.priority);
    setSubmitted(true);
  }, [account.me]);

  const profile = useMemo(() => toProfile(form), [form]);

  // After the first search, every change re-ranks (debounced). The API answers from memory, so this stays fast.
  useEffect(() => {
    if (!submitted) return;
    const timer = setTimeout(async () => {
      inflight.current?.abort();
      const ctrl = new AbortController();
      inflight.current = ctrl;
      setLoading(true);
      try {
        setData(await getRecommendations(profile, priority, ctrl.signal));
        setError("");
      } catch (e) {
        if ((e as Error).name !== "AbortError") setError((e as Error).message);
      } finally {
        if (inflight.current === ctrl) setLoading(false);
      }
    }, 250);
    return () => clearTimeout(timer);
  }, [profile, priority, submitted]);

  const shared: Shared = {
    form, setForm, submitted, submit: () => setSubmitted(true), data, loading, error, priority, setPriority, fx, courses,
  };
  const title = SECTIONS.find((s) => s.id === section)?.label ?? "";

  return (
    <div className="shell">
      <header className="mobile-bar">
        <button type="button" className="icon-btn" onClick={() => setMenuOpen(!menuOpen)} aria-label="Open menu" aria-expanded={menuOpen}>
          <Icon name="menu" />
        </button>
        <span className="brand"><Logo /> Compass</span>
        <span className="mobile-title">{title}</span>
      </header>
      <Sidebar section={section} open={menuOpen} onClose={() => setMenuOpen(false)} />
      <main className="content">
        {account.error && <div className="notice">{account.error}</div>}
        {section === "dashboard" && <Dashboard {...shared} />}
        {section === "explore" && <Explore {...shared} />}
        {section === "saved" && <Saved {...shared} />}
        {section === "applications" && <Applications {...shared} />}
        {section === "scholarships" && <Scholarships {...shared} />}
        {section === "prearrival" && <Prearrival />}
        {section === "profile" && <Profile {...shared} />}
        <footer className="site-footer">
          Guests: nothing about you is stored. Signed in: Compass stores only what you choose to save, and you can download or
          delete it any time from Profile. Anonymous usage counts help improve Compass. Always confirm fees and requirements on
          the official page before you apply.
        </footer>
      </main>
    </div>
  );
}
