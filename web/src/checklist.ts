/** Pre-arrival steps for a non-EU student coming to Ireland. Links go to official or state-run information sites. */
export interface ChecklistItem {
  id: string;
  label: string;
  when: string;
  detail: string;
  url: string;
  source: string;
}

const IMMIGRATION = { url: "https://www.irishimmigration.ie/", source: "Irish Immigration Service" };
const CITIZENS = { url: "https://www.citizensinformation.ie/", source: "Citizens Information" };

export const CHECKLIST: ChecklistItem[] = [
  { id: "accept-offer", label: "Accept your offer and pay the deposit", when: "After your offer", detail: "Your offer letter says how much and by when. Some scholarships also need the deposit paid.", url: "", source: "Your offer letter" },
  { id: "insurance", label: "Arrange private medical insurance", when: "Before the visa", detail: "Non-EU students need private medical insurance for their visa and immigration permission.", ...IMMIGRATION },
  { id: "funds", label: "Gather proof of funds", when: "Before the visa", detail: "Bank statements showing you can cover fees and living costs. Check the current amount on the official page.", ...IMMIGRATION },
  { id: "visa", label: "Apply for your Irish study visa", when: "8–12 weeks before", detail: "Indian nationals need a long-stay study visa. Apply online, then submit documents.", ...IMMIGRATION },
  { id: "accommodation", label: "Book your first weeks of accommodation", when: "Before you travel", detail: "Book something short-term, then find a longer let once you're in Ireland.", ...CITIZENS },
  { id: "flights", label: "Book flights", when: "After the visa", detail: "Aim to arrive a week before orientation.", url: "", source: "" },
  { id: "irp", label: "Register for your Irish Residence Permit (IRP)", when: "First weeks in Ireland", detail: "Book an immigration registration appointment soon after you arrive.", ...IMMIGRATION },
  { id: "pps", label: "Get a PPS number", when: "First month", detail: "Needed to work part-time and for tax.", ...CITIZENS },
  { id: "bank", label: "Open an Irish bank account", when: "First month", detail: "Most banks need proof of address and your student letter.", ...CITIZENS },
];

export const DOCUMENTS: { id: string; label: string }[] = [
  { id: "sop", label: "Statement of purpose" },
  { id: "lor1", label: "Reference letter 1" },
  { id: "lor2", label: "Reference letter 2" },
  { id: "transcripts", label: "Transcripts and degree" },
  { id: "cv", label: "CV" },
  { id: "english", label: "English test result" },
  { id: "passport", label: "Passport copy" },
];

export const STATUSES = [
  { id: "researching", label: "Researching" },
  { id: "preparing", label: "Preparing" },
  { id: "applied", label: "Applied" },
  { id: "offer", label: "Offer" },
  { id: "accepted", label: "Accepted" },
  { id: "rejected", label: "Not this time" },
] as const;
