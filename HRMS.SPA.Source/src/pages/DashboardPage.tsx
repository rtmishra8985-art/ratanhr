import { memo } from 'react';
import type { LucideIcon } from 'lucide-react';
import {
  Users,
  UserCheck,
  CalendarOff,
  Briefcase,
  Clock,
  Ticket,
  MonitorSmartphone,
  CalendarDays,
} from 'lucide-react';
import {
  useGetDashboardSummary,
  useGetAttendanceTrend,
  useGetDepartmentHeadcount,
  useGetRecentActivity,
  useGetPayrollTrend,
  useGetEmployeeDashboardStats,
} from '@workspace/api-client-react';
import { usePermissions } from '@/hooks/usePermissions';

import { PageHeader } from '@/components/layout/PageHeader';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { Button } from '@/components/ui/button';
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  Legend,
} from 'recharts';

const SummaryCard = memo(function SummaryCard({
  title,
  value,
  icon: Icon,
  loading,
  formatValue,
}: {
  title: string;
  value?: number;
  icon: LucideIcon;
  loading: boolean;
  formatValue?: (value?: number) => string | number;
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
        <Icon className="h-4 w-4 text-muted-foreground" />
      </CardHeader>
      <CardContent>
        {loading ? (
          <Skeleton className="h-8 w-16 mt-1" />
        ) : (
          <div className="text-2xl font-bold">{formatValue ? formatValue(value) : (value ?? 0)}</div>
        )}
      </CardContent>
    </Card>
  );
});

const COLORS = [
  'hsl(var(--chart-1))',
  'hsl(var(--chart-2))',
  'hsl(var(--chart-3))',
  'hsl(var(--chart-4))',
  'hsl(var(--chart-5))',
];

export default function DashboardPage() {
  // FIX: role-gate the dashboard. Previously every session (including Employee)
  // called the same admin-only endpoints (/api/dashboard/admin, /api/analytics/*,
  // /api/audit) unconditionally, so a real employee login always produced four
  // 403 responses on page load even though the RBAC enforcement itself was correct.
  // isAdmin also covers superadmin (see usePermissions.ADMIN_ROLES).
  //
  // FIX 2: wait for isLoading before branching. usePermissions() resolves
  // isAdmin=false by default until the profile query completes, so without this
  // guard every role (including Admin/SuperAdmin) briefly rendered
  // EmployeeDashboard on first paint and fired GET /api/dashboard/employee,
  // which 403s for non-employee roles.
  const { isAdmin, isLoading } = usePermissions();
  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-10 w-64" />
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {[1, 2, 3, 4].map((i) => <Skeleton key={i} className="h-24 w-full" />)}
        </div>
      </div>
    );
  }
  return isAdmin ? <AdminDashboard /> : <EmployeeDashboard />;
}

// ─── Employee dashboard ─────────────────────────────────────────────────────

function EmployeeDashboard() {
  const { data: stats, isLoading } = useGetEmployeeDashboardStats();

  return (
    <div className="space-y-6">
      <PageHeader
        title="Dashboard"
        description={stats?.fullName ? `Welcome back, ${stats.fullName}` : 'Your personal overview'}
      />

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <SummaryCard
          title="Today's Status"
          value={stats?.checkedInToday ? 1 : 0}
          icon={UserCheck}
          loading={isLoading}
          formatValue={() => (stats?.checkedInToday ? 'Checked In' : 'Not Checked In')}
        />
        <SummaryCard title="Pending Leaves" value={stats?.pendingLeaves} icon={Clock} loading={isLoading} />
        <SummaryCard title="Attendance This Month" value={stats?.attendanceDaysThisMonth} icon={CalendarOff} loading={isLoading} />
        <SummaryCard title="Upcoming Holidays" value={stats?.upcomingHolidays} icon={CalendarDays} loading={isLoading} />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <Card>
          <CardHeader>
            <CardTitle>Attendance Today</CardTitle>
            <CardDescription>Your check-in/out for today</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            {isLoading ? (
              <Skeleton className="h-16 w-full" />
            ) : (
              <>
                <p>Check-in: <span className="font-medium">{stats?.todayCheckInTime ?? '—'}</span></p>
                <p>Check-out: <span className="font-medium">{stats?.todayCheckOutTime ?? '—'}</span></p>
                <p>Hours worked: <span className="font-medium">{stats?.hoursWorkedToday ?? '—'}</span></p>
              </>
            )}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Payslip &amp; Leave</CardTitle>
            <CardDescription>Latest pay and leave usage</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2 text-sm">
            {isLoading ? (
              <Skeleton className="h-16 w-full" />
            ) : (
              <>
                <p>Last net pay: <span className="font-medium">{stats?.lastNetPay != null ? `$${stats.lastNetPay}` : '—'}</span> ({stats?.lastPayMonth ?? '—'})</p>
                <p>Leaves approved this month: <span className="font-medium">{stats?.approvedLeavesThisMonth ?? 0}</span></p>
                <p>Leaves used this year: <span className="font-medium">{stats?.totalLeavesUsedThisYear ?? 0}</span></p>
              </>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

// ─── Admin / SuperAdmin dashboard ────────────────────────────────────────────

function AdminDashboard() {
  const { isSuperAdmin } = usePermissions();
  const { data: summary, isLoading: loadingSummary } = useGetDashboardSummary();
  const { data: attendanceTrend, isLoading: loadingAttTrend } = useGetAttendanceTrend({ months: 6 });
  const { data: deptHeadcount, isLoading: loadingDepts } = useGetDepartmentHeadcount();
  // FIX: GET /api/audit is superadmin-only server-side (see AuditController's
  // [Authorize(Roles = AppRoles.SuperAdmin)]). Previously this query ran
  // unconditionally for every admin-tier session, so a plain Admin (not
  // SuperAdmin) always got a 403 on this widget. `enabled: isSuperAdmin` skips
  // the request entirely for roles that cannot use it.
  const { data: activity, isLoading: loadingActivity } = useGetRecentActivity({
    query: { enabled: isSuperAdmin },
  });
  const { data: payrollTrend, isLoading: loadingPayroll } = useGetPayrollTrend({ months: 6 });

  return (
    <div className="space-y-6">
      <PageHeader
        title="Dashboard"
        description="Overview of your organization's key metrics"
        actions={<Button>Generate Report</Button>}
      />

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
        <SummaryCard title="Total Employees"  value={summary?.totalEmployees  ?? undefined} icon={Users}              loading={loadingSummary} />
        <SummaryCard title="Present Today"    value={summary?.presentToday    ?? undefined} icon={UserCheck}          loading={loadingSummary} />
        <SummaryCard title="On Leave"         value={summary?.onLeave         ?? undefined} icon={CalendarOff}        loading={loadingSummary} />
        <SummaryCard title="Open Positions"   value={summary?.openPositions   ?? undefined} icon={Briefcase}          loading={loadingSummary} />
        <SummaryCard title="Pending Leaves"   value={summary?.pendingLeaves   ?? undefined} icon={Clock}              loading={loadingSummary} />
        <SummaryCard title="Open Tickets"     value={summary?.openTickets     ?? undefined} icon={Ticket}             loading={loadingSummary} />
        <SummaryCard title="Total Assets"     value={summary?.totalAssets     ?? undefined} icon={MonitorSmartphone}  loading={loadingSummary} />
        <SummaryCard title="Monthly Payroll"  value={summary?.monthlyPayroll  ?? undefined} icon={MonitorSmartphone}  loading={loadingSummary} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <Card className="col-span-1">
          <CardHeader>
            <CardTitle>Attendance Trend</CardTitle>
            <CardDescription>Present vs Absent over the last 6 months</CardDescription>
          </CardHeader>
          <CardContent className="h-[300px]">
            {loadingAttTrend ? (
              <Skeleton className="w-full h-full" />
            ) : attendanceTrend && attendanceTrend.length > 0 ? (
              <ResponsiveContainer width="100%" height="100%">
                <LineChart data={attendanceTrend} margin={{ top: 5, right: 20, bottom: 5, left: 0 }}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="hsl(var(--border))" />
                  <XAxis dataKey="label" stroke="hsl(var(--muted-foreground))" fontSize={12} tickLine={false} axisLine={false} />
                  <YAxis stroke="hsl(var(--muted-foreground))" fontSize={12} tickLine={false} axisLine={false} />
                  <Tooltip contentStyle={{ backgroundColor: 'hsl(var(--card))', borderColor: 'hsl(var(--border))', borderRadius: '8px' }} itemStyle={{ color: 'hsl(var(--foreground))' }} />
                  <Line type="monotone" dataKey="value" name="Present" stroke="hsl(var(--chart-1))" strokeWidth={3} dot={{ r: 4 }} activeDot={{ r: 6 }} />
                  <Line type="monotone" dataKey="secondaryValue" name="Absent" stroke="hsl(var(--chart-5))" strokeWidth={3} dot={{ r: 4 }} activeDot={{ r: 6 }} />
                </LineChart>
              </ResponsiveContainer>
            ) : (
              <div className="flex h-full items-center justify-center text-muted-foreground">No data available</div>
            )}
          </CardContent>
        </Card>

        <Card className="col-span-1">
          <CardHeader>
            <CardTitle>Department Headcount</CardTitle>
            <CardDescription>Distribution of employees by department</CardDescription>
          </CardHeader>
          <CardContent className="h-[300px]">
            {loadingDepts ? (
              <Skeleton className="w-full h-full" />
            ) : deptHeadcount && deptHeadcount.length > 0 ? (
              <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                  <Pie data={deptHeadcount} cx="50%" cy="50%" innerRadius={70} outerRadius={100} paddingAngle={2} dataKey="count" nameKey="department">
                    {deptHeadcount.map((_, index) => (
                      <Cell key={`cell-${index}`} fill={COLORS[index % COLORS.length]} />
                    ))}
                  </Pie>
                  <Tooltip contentStyle={{ backgroundColor: 'hsl(var(--card))', borderColor: 'hsl(var(--border))', borderRadius: '8px' }} />
                  <Legend verticalAlign="bottom" height={36} iconType="circle" />
                </PieChart>
              </ResponsiveContainer>
            ) : (
              <div className="flex h-full items-center justify-center text-muted-foreground">No data available</div>
            )}
          </CardContent>
        </Card>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <Card className="col-span-1 lg:col-span-2">
          <CardHeader>
            <CardTitle>Payroll Trend</CardTitle>
            <CardDescription>Monthly payroll expense over the last 6 months</CardDescription>
          </CardHeader>
          <CardContent className="h-[300px]">
            {loadingPayroll ? (
              <Skeleton className="w-full h-full" />
            ) : payrollTrend && payrollTrend.length > 0 ? (
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={payrollTrend} margin={{ top: 5, right: 20, bottom: 5, left: 20 }}>
                  <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="hsl(var(--border))" />
                  <XAxis dataKey="label" stroke="hsl(var(--muted-foreground))" fontSize={12} tickLine={false} axisLine={false} />
                  <YAxis stroke="hsl(var(--muted-foreground))" fontSize={12} tickLine={false} axisLine={false} tickFormatter={(v) => `$${v / 1000}k`} />
                  <Tooltip cursor={{ fill: 'hsl(var(--muted)/0.5)' }} contentStyle={{ backgroundColor: 'hsl(var(--card))', borderColor: 'hsl(var(--border))', borderRadius: '8px' }} formatter={(v) => [`$${v}`, 'Amount']} />
                  <Bar dataKey="value" name="Amount" fill="hsl(var(--chart-1))" radius={[4, 4, 0, 0]} maxBarSize={50} />
                </BarChart>
              </ResponsiveContainer>
            ) : (
              <div className="flex h-full items-center justify-center text-muted-foreground">No data available</div>
            )}
          </CardContent>
        </Card>

        <Card className="col-span-1">
          <CardHeader>
            <CardTitle>Recent Activity</CardTitle>
            <CardDescription>Latest updates across the platform</CardDescription>
          </CardHeader>
          <CardContent>
            {!isSuperAdmin ? (
              <div className="flex h-[200px] items-center justify-center text-center text-sm text-muted-foreground">
                Recent activity is only visible to Super Admin.
              </div>
            ) : loadingActivity ? (
              <div className="space-y-4">
                {[1, 2, 3, 4, 5].map((i) => (
                  <div key={i} className="flex gap-3">
                    <Skeleton className="h-2 w-2 rounded-full mt-2" />
                    <div className="space-y-2 flex-1">
                      <Skeleton className="h-4 w-full" />
                      <Skeleton className="h-3 w-24" />
                    </div>
                  </div>
                ))}
              </div>
            ) : activity && activity.length > 0 ? (
              <div className="space-y-6">
                {activity.map((item) => (
                  <div key={item.id} className="flex gap-3">
                    <div className="mt-1.5 h-2 w-2 rounded-full bg-primary flex-shrink-0" />
                    <div className="flex flex-col gap-1">
                      <p className="text-sm font-medium leading-none">{item.message}</p>
                      <div className="flex items-center gap-2">
                        <span className="text-xs text-muted-foreground">
                          {new Date(item.timestamp).toLocaleString()}
                        </span>
                        {item.actorName && (
                          <span className="text-xs text-muted-foreground px-1.5 py-0.5 rounded-full bg-muted">
                            {item.actorName}
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            ) : (
              <div className="flex h-[200px] items-center justify-center text-muted-foreground">
                No recent activity
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
