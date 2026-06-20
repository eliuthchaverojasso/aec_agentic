<!-- STATUS: HISTORICAL | Original date: 2026-05-26 | "Thursday demo" refers to a past client presentation. -->
<!-- Current navigation state may differ. Verify against current frontend source before relying on this. -->

# Demo Menu Cleanup (HISTORICAL — 2026-05-26)

**Original date:** 2026-05-26
**Purpose:** Define which navigation items are shown/hidden for the May 2026 client demo
**Owner:** Frontend team  

---

## Rule

**If it is not connected to real/demo data and does not support the happy path, hide it.**

For Thursday's client demo, the goal is one clean, focused walkthrough. Anything that breaks the narrative or shows incomplete features must be hidden.

---

## Navigation Structure for Demo

### Primary Menu (Top or Left Sidebar)

#### ✅ Show (Demo Happy Path)

| Menu Item | Route | Why Show | Status |
|---|---|---|---|
| **Login** | `/login` | Entry point to the app | ✅ Fully functional |
| **Project Portfolio** | `/` or `/portfolio` | Client views all projects at a glance | ✅ Fully functional |
| **Project Overview** | `/projects/{id}` | Readiness summary, key metrics | ✅ Fully functional |
| **Project Setup** | `/projects/{id}/setup` | How to create/configure a project | ✅ Fully functional |
| **Owner Requirements** | `/projects/{id}/requirements` | Core feature: requirements and evidence | ✅ Fully functional |
| **Documents / Evidence** | `/projects/{id}/documents` | Evidence list and acceptance workflow | ✅ Fully functional |
| **Deliverable Tracker / Readiness** | `/projects/{id}/readiness` | Readiness score and gap summary | ✅ Fully functional |
| **Processing / Sync** | `/processing` | Data ingestion workflow (optional, time permitting) | ✅ Fully functional |

#### ⚠️ Hide or Mark As Coming Soon (Not Core to Demo)

| Menu Item | Route | Why Hide | What to Do |
|---|---|---|---|
| **Model Viewer / Drawing Reel** | `/drawings` or similar | Not implemented yet; placeholder/screenshot-only | Hide from menu; can mention "coming soon" if asked |
| **Model Health** | `/model-health` | Depends on 3D viewer; incomplete | Hide from menu |
| **Appearance (if not working)** | `/appearance` | Smoke test passed, but only if Liquid Glass is visually perfect | Show only if theme system is fully validated |
| **Dev Mode** | `/dev` | Useful for debugging, not for client demo | Hide from menu (but keep available for team if needed) |
| **System Health / Diagnostics** | `/system-health` | Technical details; not part of client narrative | Hide from menu |
| **Debug Logs** | `/debug-logs` | Internal tool; distracts from demo | Hide from menu |

#### ❌ Do Not Show (Not Implemented or Out of Scope)

| Menu Item | Route | Why Hide | Alternative |
|---|---|---|---|
| **Admin Panel** | `/admin` | RBAC/permissions not yet needed for pilot | Hide completely |
| **User Management** | `/users` | Local demo users only, no production auth | Hide completely |
| **Settings (Global)** | `/settings` | Not configured for demo | Hide completely |
| **Compliance / Code Loader** | `/compliance` | Compliance approval is not in MVP scope | Hide completely |
| **SEION / AI Query** | `/seion` or `/ai-query` | Advisory only, not core to demo; confuses the narrative | Hide completely |
| **Reports / Export** | `/reports` | Not fully wired to demo data | Hide completely |
| **Issues** | `/issues` | Can be shown if functional; otherwise hide | If functional, can show; if not, hide |
| **Notifications** | (varies) | Not a demo feature | Hide completely |

---

## Implementation Checklist

### Frontend Code Changes

- [ ] **Navigation component** (`src/components/Layout.tsx` or similar):
  - Remove hidden routes from the nav menu.
  - Keep the routes in the code (don't delete), but don't link to them from the menu.
  - Comment why each is hidden: `// Hidden for demo: not part of happy path`

- [ ] **Route definitions** (`src/pages/` imports in main router):
  - Keep all route definitions (don't delete).
  - Add a flag or conditional to skip rendering non-demo pages: `if (isDemoMode)` or `if (!hideNonDemoPages)`.
  - Alternatively, just don't link to them from the menu; they exist but are unreachable via navigation.

- [ ] **Page components**:
  - If a page is not fully functional, wrap it with a "Coming Soon" message or remove the route entirely for demo.
  - Example: Don't remove `/drawings`, but if the viewer doesn't work, show a message: "Drawing Reel coming in Phase 2."

### Fallback Options (If Routes Are Not Cleaned Up)

If there's no time to refactor the menu, use these workarounds:

1. **CSS hide**: Use `display: none` or `visibility: hidden` to hide menu items.
   ```css
   /* Temporarily hide non-demo menu items */
   [data-nav-item="admin"],
   [data-nav-item="compliance"],
   [data-nav-item="ai-query"],
   [data-nav-item="reports"] {
     display: none;
   }
   ```

2. **Conditional rendering**: Add a feature flag.
   ```typescript
   const demoMode = true; // or read from env var
   return (
     <>
       {demoMode && <DemoMenuItems />}
       {!demoMode && <FullMenuItems />}
     </>
   );
   ```

3. **Manual routing prevention**: If someone tries to navigate to a hidden page (via URL or old bookmark), show a redirect or "Coming Soon" page.
   ```typescript
   if (demoMode && hiddenRoutes.includes(location.pathname)) {
     return <Navigate to="/" replace />;
   }
   ```

---

## Demo Day Validation

**Before the client arrives:**

- [ ] **Try every menu item shown.** Verify it loads without errors.
- [ ] **Verify no hidden menu items appear.** Check that Admin, SEION, Compliance, etc. are not in the navigation.
- [ ] **Check error boundaries.** Make sure no AppErrorBoundary is visible.
- [ ] **Test on the target browser/device.** Make sure CSS hides work on the actual screen size used.
- [ ] **Verify fallback pages load if user types a URL directly.** If someone accidentally navigates to `/admin`, they should see a "Coming Soon" or redirect, not a crash.

---

## Post-Demo Un-Hiding

After the Thursday demo and client feedback:

1. **Review client questions**: Did they ask about any hidden features?
2. **Prioritize P1 work**: If they asked about model viewer or compliance loader, move those up.
3. **Unhide features as they become ready**: Update the menu conditionals as each feature completes.
4. **Remove temporary hide flags**: Once all features are live, remove the demo-mode code.

---

## Talking Points for Client (If They Ask)

### Q: Why don't I see [feature X] in the menu?
**A:** We're showing you the core workflow first — requirements, evidence, readiness. [Feature X] is on our roadmap and coming in the next phase. We wanted to give you a clean, focused view of what's working now.

### Q: Can you show me the Model Viewer?
**A:** Not yet. The MVP is about evidence and readiness. The model viewer is coming in Phase 2. For now, we're showing you evidence sources and descriptions.

### Q: Where's the AI recommendation?
**A:** The AI features are coming later, and when they do, they'll be advisory only — they'll suggest, but your team always decides. For now, we're using deterministic readiness logic so everything is auditable and explainable.

### Q: Can we access Admin settings?
**A:** Not in this pilot. The pilot is single-user demo mode. When we deploy to Azure, we'll add role-based access and proper admin panels.

---

## Related Files

- `docs/demo/THURSDAY_DEMO_PLAN.md` — Full demo walkthrough script
- `.ai/CURRENT_STATE.md` — Current product state and boundaries
- `Pipeline/pipeline/frontend/src/components/Layout.tsx` — Main navigation component (likely)

