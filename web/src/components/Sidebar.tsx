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

interface Props {
  section: Section;
  open: boolean;
  onClose: () => void;
  collapsed: boolean;
  onToggleCollapsed: () => void;
}

export function Sidebar({ section, open, onClose, collapsed, onToggleCollapsed }: Props) {
  const { me, enabled, signOut } = useAccount();
  const counts: Partial<Record<Section, number>> = me ? { saved: me.saved.length, applications: me.applications.length } : {};

  return (
    <nav className={`sidebar ${open ? "open" : ""} ${collapsed ? "collapsed" : ""}`} aria-label="Main">
      <div className="sidebar-top">
        <a className="brand" href="#/dashboard" onClick={onClose}><Logo /> <span className="label">Compass</span></a>
        <button type="button" className="icon-btn collapse-btn" onClick={onToggleCollapsed}
                aria-label={collapsed ? "Expand sidebar" : "Collapse sidebar"} title={collapsed ? "Expand sidebar" : "Collapse sidebar"}>
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" aria-hidden="true">
            <path d={collapsed ? "M9 6l6 6-6 6" : "M15 6l-6 6 6 6"} />
          </svg>
        </button>
      </div>
      <ul>
        {SECTIONS.map((s) => (
          <li key={s.id}>
            <a href={`#/${s.id}`} className={section === s.id ? "active" : ""} title={collapsed ? s.label : undefined} aria-current={section === s.id ? "page" : undefined} onClick={onClose}>
              <Icon name={s.id} /> <span className="label">{s.label}</span>
              {counts[s.id] ? <em className="count">{counts[s.id]}</em> : null}
            </a>
          </li>
        ))}
      </ul>
      <div className="sidebar-foot">
        {collapsed ? null : me ? (
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
