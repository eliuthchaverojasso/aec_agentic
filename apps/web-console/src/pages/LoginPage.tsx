import {
  // Building2,
  // Database,
  // ShieldAlert,
  // Sparkles,
  // UserRound,
  UserRoundIcon,
} from "lucide-react";
import { type ReactNode, useMemo, useState } from "react";
import type { ProjectSummary } from "../types";
import { useAuth } from "../context/AuthContext";
import { api } from "../api/client";

export type DemoSession = {
  email: string;
  role:
    | "Executive"
    | "Project Manager"
    | "BIM/VDC"
    | "Engineer"
    | "Admin"
    | "Developer";
  projectName: string;
  environment: "Local" | "Azure Pilot";
  selectedProjectId?: number;
  createdAt: string;
};

type LoginPageProps = {
  projects: ProjectSummary[];
  onEnter: (session: DemoSession) => void;
  onOpenSystemHealth: () => void;
  onToast: (
    message: string,
    tone?: "success" | "info" | "warning",
    position?: "top" | "bottom",
  ) => void;
};

const DEMO_SESSION_KEY = "ema-ai-demo-session";

export function LoginPage({
  projects,
  onEnter,
  onOpenSystemHealth,
  onToast,
}: LoginPageProps) {
  const { login } = useAuth();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [rememberMe, setRememberMe] = useState(false);
  // const [role, setRole] = useState<DemoSession["role"]>("Executive");
  // const [projectName, setProjectName] = useState<string>("All Demo Projects");
  // const [environment, setEnvironment] =
  //   useState<DemoSession["environment"]>("Local");
  const [showPassword, setShowPassword] = useState(false);

  // const projectNames = useMemo(() => {
  //   const fromBackend = projects
  //     .map((p) => p.project_name || p.project_title)
  //     .filter(Boolean);
  //   return ["All Demo Projects", ...Array.from(new Set(fromBackend))];
  // }, [projects]);

  // const handleEnter = () => {
  //   const selectedProject = projects.find(
  //     (p) => (p.project_name || p.project_title) === projectName,
  //   );
  //   const session: DemoSession = {
  //     email,
  //     role,
  //     projectName,
  //     environment,
  //     selectedProjectId: selectedProject?.id,
  //     createdAt: new Date().toISOString(),
  //   };
  //   try {
  //     window.localStorage.setItem(DEMO_SESSION_KEY, JSON.stringify(session));
  //   } catch {
  //     // Local demo session persistence is best-effort.
  //   }
  //   onEnter(session);
  // };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);

    try {
      const data = await api.login({ email, password });
      login(data.access_token, data.user, rememberMe);
      window.location.href = "/";
    } catch (err: any) {
      const rawError = err.message || "";

      onToast(rawError, "warning", "top");
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="flex min-h-screen flex-1 flex-col justify-center px-6 py-12 lg:px-8 bg-white relative">
      <div className="sm:mx-auto sm:w-full sm:max-w-sm">
        <h2 className="text-3xl font-semibold tracking-tight text-gray-950">
          Welcome back
        </h2>
        <p className="mt-2 text-sm text-gray-[#64748B]">
          Sign in to your account to continue
        </p>

        <form className="mt-8 space-y-5" onSubmit={handleSubmit}>
          <div>
            <label
              htmlFor="email"
              className="block text-sm font-medium leading-6 text-gray-950"
            >
              Email
            </label>
            <div className="relative mt-2 rounded-md shadow-sm">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
                <UserRoundIcon
                  className="h-5 w-5 "
                  fill="#94A3B8"
                  color="#94A3B8"
                />
              </div>
              <input
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="Enter your email"
                className="block w-full rounded-md border-0 py-2.5 pl-10 pr-4 text-gray-900 bg-gray-50 ring-1 ring-inset ring-gray-200 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-[#159B8E] sm:text-sm sm:leading-6 outline-none"
              />
            </div>
          </div>

          <div>
            <label
              htmlFor="password"
              className="block text-sm font-medium leading-6 text-gray-950"
            >
              Password
            </label>
            <div className="relative mt-2 rounded-md shadow-sm">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3">
                <svg
                  className="h-5 w-5 text-[#94A3B8]"
                  viewBox="0 0 20 20"
                  fill="currentColor"
                >
                  <path
                    fillRule="evenodd"
                    d="M10 1a4.5 4.5 0 00-4.5 4.5V9H5a2 2 0 00-2 2v6a2 2 0 002 2h10a2 2 0 002-2v-6a2 2 0 00-2-2h-.5V5.5A4.5 4.5 0 0010 1zm3 8V5.5a3 3 0 10-6 0V9h6z"
                    clipRule="evenodd"
                  />
                </svg>
              </div>
              <input
                id="password"
                name="password"
                type={showPassword ? "text" : "password"}
                autoComplete="current-password"
                required
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Enter your password"
                className="block w-full rounded-md border-0 py-2.5 pl-10 pr-10 text-gray-900 bg-gray-50 ring-1 ring-inset ring-gray-200 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-[#159B8E] sm:text-sm sm:leading-6 outline-none"
              />
              <button
                type="button"
                onClick={() => setShowPassword(!showPassword)}
                className="absolute inset-y-0 right-0 flex items-center pr-3 text-gray-400 hover:text-gray-600"
              >
                {showPassword ? (
                  <svg
                    className="h-5 w-5 text-[#94A3B8]"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M15 12a3 3 0 11-6 0 3 3 0 016 0z"
                    />
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z"
                    />
                  </svg>
                ) : (
                  <svg
                    className="h-5 w-5 text-[#94A3B8]"
                    fill="none"
                    viewBox="0 0 24 24"
                    stroke="currentColor"
                  >
                    <path
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      strokeWidth={2}
                      d="M13.875 18.825A10.05 10.05 0 0112 19c-4.478 0-8.268-2.943-9.543-7a9.97 9.97 0 011.563-3.029m5.858.908a3 3 0 114.243 4.243M9.878 9.878l4.242 4.242M9.88 9.88l-3.29-3.29m7.532 7.532l3.29 3.29M3 3l18 18"
                    />
                  </svg>
                )}
              </button>
            </div>
          </div>

          <div className="flex items-center">
            <input
              id="remember-me"
              name="remember-me"
              type="checkbox"
              checked={rememberMe}
              onChange={(e) => setRememberMe(e.target.checked)}
              className="h-4 w-4 rounded border-gray-300 accent-[#159B8E] focus:ring-[#159B8E] cursor-pointer"
            />
            <label
              htmlFor="remember-me"
              className="ml-3 block text-sm text-[#475569] select-none"
            >
              Remember me
            </label>
          </div>

          <div>
            <button
              type="submit"
              className="flex w-full items-center justify-center gap-2 rounded-md bg-[#159B8E] px-4 py-2.5 text-sm font-semibold text-white shadow-sm hover:bg-[#12867B] focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#159B8E] transition duration-150 ease-in-out"
            >
              Sign In
              <svg
                className="h-4 w-4"
                fill="none"
                viewBox="0 0 24 24"
                strokeWidth="2.5"
                stroke="currentColor"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3"
                />
              </svg>
            </button>
          </div>
        </form>
      </div>

      <div className="fixed bottom-4 right-4 flex items-center gap-2 p-2">
        <span className="flex items-center justify-center rounded bg-[#CCFBF7] p-2.5">
          <svg
            width="18"
            height="18"
            viewBox="0 0 18 18"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
          >
            <path
              d="M8.24414 0.200394C8.73281 0.0246124 9.26719 0.0246124 9.75937 0.200394L16.5094 2.61211C17.4023 2.93203 18 3.7793 18 4.73203V13.268C18 14.2172 17.4023 15.068 16.5059 15.3879L9.75586 17.7996C9.26719 17.9754 8.73281 17.9754 8.24063 17.7996L1.49063 15.3879C0.597656 15.068 0 14.2207 0 13.268V4.73203C0 3.78282 0.597656 2.93203 1.49414 2.61211L8.24414 0.200394ZM9 2.32032L2.89336 4.5L9 6.67969L15.1066 4.5L9 2.32032ZM10.125 15.2789L15.75 13.2715V6.66211L10.125 8.66953V15.2789Z"
              fill="#0D9488"
            />
          </svg>
        </span>
        <span className="text-xl font-semibold text-[#0D9488] select-none">
          EMA AI Engineering
        </span>
      </div>
    </div>
    // <div className="min-h-screen bg-canvas px-4 py-8">
    //   <div className="mx-auto grid w-full max-w-6xl gap-6 lg:grid-cols-[1.15fr_0.85fr]">
    //     <section className="ema-card ema-glass-panel p-8">
    //       <div className="flex items-center gap-3 text-accent">
    //         <Building2 size={24} />
    //         <span className="text-sm font-semibold uppercase tracking-wide">EMA AI Engineering</span>
    //       </div>
    //       <h1 className="mt-4 text-4xl font-semibold text-ink">Engineering Intelligence for DD/CD Readiness</h1>
    //       <p className="mt-3 max-w-2xl text-sm text-muted">
    //         Local Demo · Not Production · Not Official Compliance
    //       </p>
    //       <div className="mt-6 grid gap-3 sm:grid-cols-3">
    //         <InfoPill icon={Database} label="PostgreSQL Source of Truth" />
    //         <InfoPill icon={ShieldAlert} label="Operator Controlled Ingest" />
    //         <InfoPill icon={Sparkles} label="AI Advisory Only" />
    //       </div>
    //       <div className="mt-8 rounded-lg border border-line bg-surface-2 p-4">
    //         <p className="text-xs text-muted">
    //           This local demo does not provide production authentication or authorization. Settings and session state are stored locally in this browser.
    //         </p>
    //       </div>
    //     </section>

    //     <section className="ema-card p-6">
    //       <h2 className="text-xl font-semibold text-ink">Enter Local Demo</h2>
    //       <div className="mt-4 space-y-3">
    //         <Field label="Email">
    //           <input className="ema-input h-10 w-full px-3" value={email} onChange={(e) => setEmail(e.target.value)} />
    //         </Field>
    //         <Field label="Role">
    //           <select className="ema-input h-10 w-full px-3" value={role} onChange={(e) => setRole(e.target.value as DemoSession["role"])}>
    //             {["Executive", "Project Manager", "BIM/VDC", "Engineer", "Admin", "Developer"].map((item) => (
    //               <option key={item} value={item}>{item}</option>
    //             ))}
    //           </select>
    //         </Field>
    //         <Field label="Project">
    //           <select className="ema-input h-10 w-full px-3" value={projectName} onChange={(e) => setProjectName(e.target.value)}>
    //             {projectNames.map((name) => (
    //               <option key={name} value={name}>{name}</option>
    //             ))}
    //           </select>
    //         </Field>
    //         <Field label="Environment">
    //           <select className="ema-input h-10 w-full px-3" value={environment} onChange={(e) => setEnvironment(e.target.value as DemoSession["environment"])}>
    //             <option value="Local">Local</option>
    //             <option value="Azure Pilot">Azure Pilot (Placeholder)</option>
    //           </select>
    //         </Field>
    //       </div>
    //       <div className="mt-5 flex flex-wrap gap-2">
    //         <button type="button" className="ema-btn-primary" onClick={handleEnter}>Enter Local Demo</button>
    //         <button type="button" className="ema-btn-secondary" onClick={onOpenSystemHealth}>Open System Health</button>
    //         <button type="button" className="ema-btn-secondary" onClick={() => {
    //           setEmail("alex@ema.ai");
    //           setRole("Executive");
    //           setProjectName("All Demo Projects");
    //           setEnvironment("Local");
    //         }}>
    //           Continue as Alex Director
    //         </button>
    //       </div>
    //     </section>
    //   </div>
    // </div>
  );
}

function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1 block text-xs font-semibold uppercase tracking-wide text-muted">
        {label}
      </span>
      {children}
    </label>
  );
}

function InfoPill({
  icon: Icon,
  label,
}: {
  icon: React.ComponentType<{ size?: number }>;
  label: string;
}) {
  return (
    <div className="ema-pill bg-surface">
      <Icon size={12} />
      {label}
    </div>
  );
}
