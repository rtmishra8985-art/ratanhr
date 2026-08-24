/**
 * App.tsx — Root application component.
 *
 * Features:
 *  - Double ErrorBoundary (outer catches provider failures, inner catches page failures).
 *  - All pages loaded with React.lazy + Suspense (route-level code splitting).
 *  - QueryClient configured with sensible production defaults.
 *
 * Fixed: B2  — added /reports route + lazy import
 * Fixed: M1  — added /training route
 * Fixed: M2  — added /expenses route
 * Fixed: M3  — no route needed; MFA is embedded in SettingsPage + LoginPage
 * Fixed: M5  — added /org-chart route
 * Fixed: M6  — added /travel route
 * Fixed: M7  — added /onboarding route
 * Fixed: U3  — ReactQueryDevtools added (DEV only)
 * Fixed: U4  — RecruitmentPage wrapped in its own ErrorBoundary
 * HOTFIX P2: added /biometric/devices route (BiometricDevicesPage)
 * HOTFIX P2: added /designations route (DesignationPage)
 * HOTFIX P3: added /analytics route (AnalyticsPage)
 * HOTFIX P3: added /employees/:id/transfers, /employees/:id/promotions, /employees/:id/exit routes
 * Fix M-05: added /payroll/bonuses-deductions route (BonusDeductionPage)
 * Fix L-01: added /audit-log route (AuditLogPage)
 * Fix GAP-01: added /sales route (SalesPage) — Sales/CRM frontend was entirely missing
 */

import { lazy, Suspense } from 'react';
import { ThemeProvider }       from 'next-themes';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools }  from '@tanstack/react-query-devtools';
import { Toaster }             from '@/components/ui/toaster';
import { Toaster as SonnerToaster } from '@/components/ui/sonner';
import { TooltipProvider }     from '@/components/ui/tooltip';
import { Route, Switch, Router as WouterRouter } from 'wouter';
import { AuthProvider }        from './contexts/AuthContext';
import { Layout }              from './components/layout/Layout';
import { GuestGuard }          from './components/layout/GuestGuard';
import { ErrorBoundary }       from './components/ErrorBoundary';

// ─── Route-level code splitting ───────────────────────────────────────────────

const LoginPage        = lazy(() => import('./pages/LoginPage'));
const ForgotPasswordPage = lazy(() => import('./pages/ForgotPasswordPage'));
const ResetPasswordPage  = lazy(() => import('./pages/ResetPasswordPage'));
const DashboardPage    = lazy(() => import('./pages/DashboardPage'));
const EmployeesPage    = lazy(() => import('./pages/employees/EmployeesPage'));
const EmployeeDetailPage = lazy(() => import('./pages/employees/EmployeeDetailPage'));
const AttendancePage   = lazy(() => import('./pages/AttendancePage'));
const LeavePage        = lazy(() => import('./pages/LeavePage'));
const PayrollPage      = lazy(() => import('./pages/PayrollPage'));
const RecruitmentPage  = lazy(() => import('./pages/recruitment/RecruitmentPage'));
const PerformancePage  = lazy(() => import('./pages/performance/PerformancePage'));
const AssetsPage       = lazy(() => import('./pages/assets/AssetsPage'));
const HelpdeskPage     = lazy(() => import('./pages/helpdesk/HelpdeskPage'));
const SettingsPage     = lazy(() => import('./pages/SettingsPage'));
const ReportsPage      = lazy(() => import('./pages/ReportsPage'));
const OrgChartPage     = lazy(() => import('./pages/OrgChartPage'));
const TrainingPage     = lazy(() => import('./pages/training/TrainingPage'));
const ExpensesPage     = lazy(() => import('./pages/expenses/ExpensesPage'));
const TravelPage       = lazy(() => import('./pages/travel/TravelPage'));
const OnboardingPage   = lazy(() => import('./pages/onboarding/OnboardingPage'));
const NotFound         = lazy(() => import('./pages/not-found'));
const TimesheetPage    = lazy(() => import('./pages/timesheet/TimesheetPage'));
// Restored: missing Organisation modules (SEC-FIX P3)
const ShiftPage        = lazy(() => import('./pages/shifts/ShiftPage'));
const BiometricPage    = lazy(() => import('./pages/biometric/BiometricPage'));
const DepartmentPage   = lazy(() => import('./pages/departments/DepartmentPage'));
const HolidayPage      = lazy(() => import('./pages/holidays/HolidayPage'));
const BiometricDevicesPage = lazy(() => import('./pages/biometric/BiometricDevicesPage'));
const DesignationPage      = lazy(() => import('./pages/departments/DesignationPage'));
const AnalyticsPage          = lazy(() => import('./pages/AnalyticsPage'));
const EmployeeTransferPage   = lazy(() => import('./pages/employees/EmployeeTransferPage'));
const EmployeePromotionPage  = lazy(() => import('./pages/employees/EmployeePromotionPage'));
const EmployeeExitPage       = lazy(() => import('./pages/employees/EmployeeExitPage'));
// Fix M-05: Bonuses & Deductions standalone page
const BonusDeductionPage     = lazy(() => import('./pages/payroll/BonusDeductionPage'));
// Fix L-01: Audit Log viewer
const AuditLogPage           = lazy(() => import('./pages/AuditLogPage'));
// Fix: Sales/CRM module — frontend was entirely missing despite full backend implementation
const SalesPage              = lazy(() => import('./pages/sales/SalesPage'));
// Fix: Sidebar "Webhooks" nav item linked to a nonexistent static /webhooks.html file —
// WebhookController.cs fully implements list/register/delete but had no frontend page.
const WebhooksPage           = lazy(() => import('./pages/WebhooksPage'));

// ─── QueryClient ──────────────────────────────────────────────────────────────

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 1000 * 60 * 5,   // 5 minutes
    },
  },
});

// ─── Loading spinners ─────────────────────────────────────────────────────────

function FullPageSpinner() {
  return (
    <div className="flex h-screen w-full items-center justify-center" aria-label="Loading">
      <div className="animate-spin h-8 w-8 border-4 border-primary border-t-transparent rounded-full" />
    </div>
  );
}

function PageSpinner() {
  return (
    <div className="flex h-[50vh] w-full items-center justify-center" aria-label="Loading page">
      <div className="animate-spin h-8 w-8 border-4 border-primary border-t-transparent rounded-full" />
    </div>
  );
}

// ─── Router ───────────────────────────────────────────────────────────────────

function AppRouter() {
  return (
    <Suspense fallback={<FullPageSpinner />}>
      <Switch>
        <Route path="/login">
          {/* BUGFIX: an already-authenticated user navigating to /login directly
              (URL bar, bookmark, browser back/forward) previously just saw the
              login form again instead of being redirected to /dashboard. */}
          <GuestGuard>
            <LoginPage />
          </GuestGuard>
        </Route>
        {/* FIX: forgot/reset password pages were entirely missing on the frontend
            despite full backend support (POST /api/auth/forgot-password,
            POST /api/auth/reset-password) — the reset email linked to a page
            that never existed as an SPA route. Both must be reachable while
            unauthenticated, alongside /login, outside the authenticated Layout. */}
        <Route path="/forgot-password">
          <GuestGuard>
            <ForgotPasswordPage />
          </GuestGuard>
        </Route>
        <Route path="/reset-password">
          <GuestGuard>
            <ResetPasswordPage />
          </GuestGuard>
        </Route>
        <Route path="/:rest*">
          <Layout>
            <Suspense fallback={<PageSpinner />}>
              <Switch>
                <Route path="/"              component={DashboardPage} />
                <Route path="/dashboard"     component={DashboardPage} />
                <Route path="/employees"     component={EmployeesPage} />
                {/* HOTFIX P3: employee sub-pages — must be ordered before /employees/:id */}
                <Route path="/employees/:id/transfers"  component={EmployeeTransferPage} />
                <Route path="/employees/:id/promotions" component={EmployeePromotionPage} />
                <Route path="/employees/:id/exit"       component={EmployeeExitPage} />
                <Route path="/employees/:id" component={EmployeeDetailPage} />
                <Route path="/attendance"    component={AttendancePage} />
                <Route path="/timesheet"     component={TimesheetPage} />
                <Route path="/leave"         component={LeavePage} />
                {/* Fix M-05: more-specific /payroll/bonuses-deductions before /payroll */}
                <Route path="/payroll/bonuses-deductions" component={BonusDeductionPage} />
                <Route path="/payroll"       component={PayrollPage} />
                <Route path="/recruitment">
                  {/* Fixed: U4 — page-level ErrorBoundary for RecruitmentPage */}
                  <ErrorBoundary>
                    <RecruitmentPage />
                  </ErrorBoundary>
                </Route>
                <Route path="/performance"  component={PerformancePage} />
                <Route path="/assets"       component={AssetsPage} />
                <Route path="/helpdesk"     component={HelpdeskPage} />
                <Route path="/reports"      component={ReportsPage} />
                <Route path="/org-chart"    component={OrgChartPage} />
                <Route path="/training"     component={TrainingPage} />
                <Route path="/expenses"     component={ExpensesPage} />
                <Route path="/travel"       component={TravelPage} />
                <Route path="/onboarding"   component={OnboardingPage} />
                <Route path="/shifts"       component={ShiftPage} />
                {/* HOTFIX P2: /biometric/devices before /biometric so the more-specific route wins */}
                <Route path="/biometric/devices" component={BiometricDevicesPage} />
                <Route path="/biometric"   component={BiometricPage} />
                <Route path="/departments" component={DepartmentPage} />
                {/* HOTFIX P2: Designation management page */}
                <Route path="/designations" component={DesignationPage} />
                <Route path="/holidays"    component={HolidayPage} />
                {/* HOTFIX P3: Analytics page */}
                {/* Fix: Sales/CRM module route — was entirely missing */}
                <Route path="/sales"       component={SalesPage} />
                <Route path="/analytics"   component={AnalyticsPage} />
                {/* Fix L-01: Audit log viewer */}
                <Route path="/audit-log"   component={AuditLogPage} />
                {/* Fix: Sidebar "Webhooks" link previously pointed to a nonexistent static file */}
                <Route path="/webhooks"    component={WebhooksPage} />
                <Route path="/settings"     component={SettingsPage} />
                <Route component={NotFound} />
              </Switch>
            </Suspense>
          </Layout>
        </Route>
      </Switch>
    </Suspense>
  );
}

// ─── Root ─────────────────────────────────────────────────────────────────────

function App() {
  return (
    <ErrorBoundary>
      <ThemeProvider attribute="class" defaultTheme="light" enableSystem={false}>
        <QueryClientProvider client={queryClient}>
          <WouterRouter base={import.meta.env.BASE_URL.replace(/\/$/, '')}>
            <AuthProvider>
              <TooltipProvider>
                <ErrorBoundary>
                  <AppRouter />
                </ErrorBoundary>
                <Toaster />
                {/* BUGFIX: 13 pages (LeavePage, SalesPage, ShiftPage, HolidayPage,
                    TimesheetPage, OnboardingPage, EmployeeTransferPage,
                    EmployeePromotionPage, EmployeeExitPage, DepartmentPage,
                    DesignationPage, BiometricDevicesPage) call toast.success()/
                    toast.error() from the 'sonner' package, but only the radix-based
                    <Toaster> (@/components/ui/toaster, backed by useToast()) was ever
                    mounted here. Sonner's own <Toaster> (@/components/ui/sonner) was
                    defined but never rendered, so every sonner toast call silently did
                    nothing - mutations succeeded on the backend with zero visual
                    confirmation to the user (success or error) on those 13 pages. */}
                <SonnerToaster />
              </TooltipProvider>
            </AuthProvider>
          </WouterRouter>
          {/* Fixed: U3 — ReactQueryDevtools, DEV only */}
          {import.meta.env.DEV && <ReactQueryDevtools initialIsOpen={false} />}
        </QueryClientProvider>
      </ThemeProvider>
    </ErrorBoundary>
  );
}

export default App;
