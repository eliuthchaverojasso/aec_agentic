export type BrandConfig = {
  appName: string;
  productName: string;
  companyName: string;
  tagline: string;
  whiteLabelMode: boolean;
  nav: {
    globalOverview: string;
    globalProjects: string;
    globalIssues: string;
    globalReports: string;
  };
  support: {
    docsLabel: string;
    supportLabel: string;
  };
  colors: {
    teal900: string;
    teal800: string;
    teal700: string;
    tealSoft: string;
    gold700: string;
    gold600: string;
    goldSoft: string;
    background: string;
    ink: string;
    muted: string;
  };
  featureFlags: {
    showPrototypeData: boolean;
    showEvidenceCandidates: boolean;
    showSeionAdvisory: boolean;
    showModelHealthFallback: boolean;
    enableDevMode: boolean;
    enableLocalDemoUsers: boolean;
    enableAppearanceControls: boolean;
  };
};

export const brandConfig: BrandConfig = {
  appName: "EMA AI Engineering",
  productName: "EMA AI",
  companyName: "EMA Engineering",
  tagline: "Design. Solve. Enhance.",
  whiteLabelMode: true,
  nav: {
    globalOverview: "Executive Overview",
    globalProjects: "Projects Portfolio",
    globalIssues: "Enterprise Issues",
    globalReports: "Reports",
  },
  support: {
    docsLabel: "Documentation",
    supportLabel: "Support",
  },
  colors: {
    teal900: "#1E6A5A",
    teal800: "#236F62",
    teal700: "#2E7D70",
    tealSoft: "#DDEDEA",
    gold700: "#C9A86A",
    gold600: "#D6B77D",
    goldSoft: "#F2E6CC",
    background: "#EEF2F2",
    ink: "#162622",
    muted: "#657A75",
  },
  featureFlags: {
    showPrototypeData: true,
    showEvidenceCandidates: true,
    showSeionAdvisory: true,
    showModelHealthFallback: true,
    enableDevMode: true,
    enableLocalDemoUsers: true,
    enableAppearanceControls: true,
  },
};
