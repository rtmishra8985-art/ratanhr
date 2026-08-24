// Wired to existing: GET /api/analytics/headcount, /attendance, /payroll, /turnover
// SEC: All API calls use credentials: 'include' (cookie-based auth). Admin/superadmin only.
import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { BarChart3, TrendingUp, Users, DollarSign, ArrowUpDown } from 'lucide-react';

import { PageHeader }  from '@/components/layout/PageHeader';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Skeleton } from '@/components/ui/skeleton';
import { csrfFetch } from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL?.replace(/\/$/, '') ?? '';

interface HeadcountData {
  totalEmployees: number;
  activeEmployees: number;
  inactiveEmployees: number;
  newJoiners: number;
  byDepartment: { department: string; active: number; inactive: number }[];
}

interface AttendanceData {
  averageAttendancePct: number;
  totalPresent: number;
  totalAbsent: number;
  totalLate: number;
}

interface PayrollData {
  totalCost: number;
  avgMonthlyCost: number;
}

interface TurnoverData {
  turnoverRatePct: number;
  exits: number;
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function apiFetch(path: string) {
  return csrfFetch(`${BASE}${path}`, { credentials: 'include' }).then(r => {
    if (!r.ok) throw new Error(`API error ${r.status}`);
    return r.json().then((d: { data?: unknown }) => d?.data ?? d);
  });
}

const currentYear  = new Date().getFullYear();
const currentMonth = new Date().toISOString().slice(0, 7);

// ─── Stat card ────────────────────────────────────────────────────────────────

function StatCard({ title, value, icon: Icon, sub }: { title: string; value: string | number; icon: React.ElementType; sub?: string }) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2 space-y-0">
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
        <Icon className="h-4 w-4 text-muted-foreground" />
      </CardHeader>
      <CardContent>
        <div className="text-2xl font-bold">{value}</div>
        {sub && <p className="text-xs text-muted-foreground mt-1">{sub}</p>}
      </CardContent>
    </Card>
  );
}

// ─── Component ────────────────────────────────────────────────────────────────

export default function AnalyticsPage() {
  const [year,   setYear]  = useState(String(currentYear));
  const [period, setPeriod]= useState(currentMonth);

  // Headcount
  const { data: headcount, isLoading: hcLoading } = useQuery({
    queryKey: ['analytics-headcount', year],
    queryFn: () => apiFetch(`/api/analytics/headcount?year=${year}`),
  });

  // Attendance summary
  const { data: attendance, isLoading: attLoading } = useQuery({
    queryKey: ['analytics-attendance', period],
    queryFn: () => apiFetch(`/api/analytics/attendance?period=${period}`),
  });

  // Payroll cost
  const { data: payroll, isLoading: payLoading } = useQuery({
    queryKey: ['analytics-payroll', year],
    queryFn: () => apiFetch(`/api/analytics/payroll?year=${year}`),
  });

  // Turnover
  const { data: turnover, isLoading: trnLoading } = useQuery({
    queryKey: ['analytics-turnover', year],
    queryFn: () => apiFetch(`/api/analytics/turnover?year=${year}`),
  });

  const yearOptions = [currentYear, currentYear - 1, currentYear - 2].map(String);

  // Generate month options for the last 12 months
  const monthOptions: string[] = [];
  for (let i = 0; i < 12; i++) {
    const d = new Date();
    d.setMonth(d.getMonth() - i);
    monthOptions.push(d.toISOString().slice(0, 7));
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Analytics"
        description="Workforce insights and key HR metrics."
      />

      {/* Year / Period selectors */}
      <div className="flex flex-wrap gap-4">
        <div className="flex flex-col gap-1">
          <span className="text-xs text-muted-foreground">Year</span>
          <Select value={year} onValueChange={setYear}>
            <SelectTrigger className="w-[120px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {yearOptions.map(y => <SelectItem key={y} value={y}>{y}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        <div className="flex flex-col gap-1">
          <span className="text-xs text-muted-foreground">Attendance Period</span>
          <Select value={period} onValueChange={setPeriod}>
            <SelectTrigger className="w-[160px]">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              {monthOptions.map(m => <SelectItem key={m} value={m}>{m}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Headcount */}
      <div>
        <h2 className="text-base font-semibold mb-3">Headcount — {year}</h2>
        {hcLoading
          ? <div className="grid grid-cols-2 md:grid-cols-4 gap-4">{Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-24 rounded-xl" />)}</div>
          : <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <StatCard title="Total Employees" value={(headcount as HeadcountData | undefined)?.totalEmployees ?? '—'} icon={Users} />
              <StatCard title="Active" value={(headcount as HeadcountData | undefined)?.activeEmployees ?? '—'} icon={Users} sub="Currently employed" />
              <StatCard title="Inactive" value={(headcount as HeadcountData | undefined)?.inactiveEmployees ?? '—'} icon={Users} sub="On leave / exited" />
              <StatCard title="New Joiners" value={(headcount as HeadcountData | undefined)?.newJoiners ?? '—'} icon={TrendingUp} sub={`In ${year}`} />
            </div>
        }

        {/* Department breakdown */}
        {!hcLoading && ((headcount as HeadcountData | undefined)?.byDepartment ?? []).length > 0 && (
          <div className="mt-4 rounded-md border">
            <table className="w-full text-sm">
              <thead className="bg-muted/50">
                <tr>
                  <th className="text-left px-4 py-2 font-medium">Department</th>
                  <th className="text-right px-4 py-2 font-medium">Active</th>
                  <th className="text-right px-4 py-2 font-medium">Inactive</th>
                </tr>
              </thead>
              <tbody>
                {((headcount as HeadcountData | undefined)?.byDepartment ?? []).map((d) => (
                  <tr key={d.department} className="border-t">
                    <td className="px-4 py-2">{d.department}</td>
                    <td className="px-4 py-2 text-right">{d.active}</td>
                    <td className="px-4 py-2 text-right text-muted-foreground">{d.inactive}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Attendance */}
      <div>
        <h2 className="text-base font-semibold mb-3">Attendance — {period}</h2>
        {attLoading
          ? <div className="grid grid-cols-2 md:grid-cols-4 gap-4">{Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-24 rounded-xl" />)}</div>
          : <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <StatCard title="Avg. Attendance %" value={`${(attendance as AttendanceData | undefined)?.averageAttendancePct ?? '—'}%`} icon={BarChart3} />
              <StatCard title="Present Days" value={(attendance as AttendanceData | undefined)?.totalPresent ?? '—'} icon={BarChart3} />
              <StatCard title="Absent Days" value={(attendance as AttendanceData | undefined)?.totalAbsent ?? '—'} icon={BarChart3} />
              <StatCard title="Late Arrivals" value={(attendance as AttendanceData | undefined)?.totalLate ?? '—'} icon={BarChart3} />
            </div>
        }
      </div>

      {/* Payroll + Turnover */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <div>
          <h2 className="text-base font-semibold mb-3">Payroll Cost — {year}</h2>
          {payLoading
            ? <Skeleton className="h-24 rounded-xl" />
            : <div className="grid grid-cols-2 gap-4">
                <StatCard title="Total Payroll Cost" value={`₹${((payroll as PayrollData | undefined)?.totalCost ?? 0).toLocaleString('en-IN')}`} icon={DollarSign} />
                <StatCard title="Avg Monthly Cost" value={`₹${((payroll as PayrollData | undefined)?.avgMonthlyCost ?? 0).toLocaleString('en-IN')}`} icon={DollarSign} />
              </div>
          }
        </div>
        <div>
          <h2 className="text-base font-semibold mb-3">Turnover — {year}</h2>
          {trnLoading
            ? <Skeleton className="h-24 rounded-xl" />
            : <div className="grid grid-cols-2 gap-4">
                <StatCard title="Turnover Rate" value={`${(turnover as TurnoverData | undefined)?.turnoverRatePct ?? '—'}%`} icon={ArrowUpDown} />
                <StatCard title="Exits" value={(turnover as TurnoverData | undefined)?.exits ?? '—'} icon={ArrowUpDown} sub={`In ${year}`} />
              </div>
          }
        </div>
      </div>
    </div>
  );
}
