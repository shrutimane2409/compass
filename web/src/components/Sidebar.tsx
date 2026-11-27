import { useAccount } from "../account";
import { GoogleButton } from "./GoogleButton";
import { Icon, Logo } from "./Icons";

export type Section = "dashboard" | "explore" | "saved" | "applications" | "scholarships" | "prearrival" | "profile";

export const SECTIONS: { id: Section; label: string }[] = [
  { id: "dashboard", label: "Dashboard" },
  { id: "explore", label: "Explore courses" },
  { id: "saved", label: "Saved" },
  { id: "applications", label: "Applications" },
  { id: "scholarships", label: "Scholarships" },
  { id: "prearrival", label: "Pre-arrival" },
  { id: "profile", label: "Profile" },
];

export function Sidebar({ section, open, onClose }: { section: Section; open: boolean; onClose: () => void }) {
  const { me, enabled, signOut } = useAccount();
  const counts: Partial<Record<Section, number>> = me ? { saved: me.saved.length, applications: me.applications.length } : {};

  return (
    <nav className={`sidebar ${open ? "open" : ""}`} aria-label="Main">
      <a className="brand" href="#/dashboard" onClick={onClose}><Logo /> Compass</a>
      <ul>
        {SECTIONS.map((s) => (
          <li key={s.id}>
            <a href={`#/${s.id}`} className={section === s.id ? "active" : ""} aria-current={section === s.id ? "page" : undefined} onClick={onClose}>
              <Icon name={s.id} /> <span>{s.label}</span>
              {counts[s.id] ? <em className="count">{counts[s.id]}</em> : null}
            </a>
          </li>
        ))}
      </ul>
      <div className="sidebar-foot">
        {me ? (
          <div className="who">
            {me.user.pictureUrl
              ? <img src={me.user.pictureUrl} alt="" referrerPolicy="no-referrer" />
              : <span className="avatar">{(me.user.name ?? me.user.email)[0].toUpperCase()}</span>}
            <div>
              <b>{me.user.name ?? me.user.email}</b>
              <button type="button" className="link small" onClick={signOut}>Sign out</button>
            </div>
          </div>
        ) : enabled ? (
          <div className="guest">
            <p>Sign in to save your profile, courses and progress.</p>
            <GoogleButton width={208} />
          </div>
        ) : (
          <p className="guest">Using Compass as a guest.</p>
        )}
      </div>
    </nav>
  );
}
