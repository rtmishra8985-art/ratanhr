import { useState } from 'react';
import { Download, FileSpreadsheet } from 'lucide-react';
import { PageHeader }  from '@/components/layout/PageHeader';
import { Button }      from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { Input }       from '@/components/ui/input';
import { Label }       from '@/components/ui/label';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import { useToast }    from '@/hooks/use-toast';
import { csrfFetch }  from '@/utils/csrfFetch';

const BASE = import.meta.env.BASE_URL.replace(/\/$/, '');

// ─── Shared types ─────────────────────────────────────────────────────────────

interface DateRange { from: string; to: string; }

function useDateRange() {
  const today = new Date().toISOString().split('T')[0];
  const firstOfMonth = today.slice(0, 8) + '01';
  return useState<DateRange>({ from: firstOfMonth, to: today });
}

// ─── Generic date-range controls (used by non-payroll tabs) ───────────────────

function DateRangePicker({
  range, onChange,
}: { range: DateRange; onChange: (r: DateRange) => void }) {
  return (
    <div className="flex flex-wrap items-end gap-4">
      <div className="space-y-1">
        <Label htmlFor="from">From</Label>
        <Input
          id="from" type="date" value={range.from}
          onChange={(e) => onChange({ ...range, from: e.target.value })}
          className="w-40"
        />
      </div>
      <div className="space-y-1">
        <Label htmlFor="to">To</Label>
        <Input
          id="to" type="date" value={range.to}
          onChange={(e) => onChange({ ...range, to: e.target.value })}
          className="w-40"
        />
      </div>
    </div>
  );
}

// ─── Generic export button (attendance / leave / employee / salary) ───────────

function ExportButton({
  endpoint, label, range,
}: { endpoint: string; label: string; range: DateRange }) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);

  const handleExport = async () => {
    setLoading(true);
    try {
      const url = `${BASE}${endpoint}?from=${range.from}&to=${range.to}&format=excel`;
      const res = await csrfFetch(url, { credentials: 'include' });
      if (!res.ok) throw new Error(await res.text());
      const blob = await res.blob();
      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = `${label.replace(/\s+/g, '_')}_${range.from}_${range.to}.xlsx`;
      link.click();
      URL.revokeObjectURL(link.href);
    } catch (err: unknown) {
      toast({ title: 'Export failed', description: String(err), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Button variant="outline" size="sm" onClick={handleExport} disabled={loading}>
      {loading ? (
        <span className="flex items-center gap-2">
          <span className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          Exporting…
        </span>
      ) : (
        <span className="flex items-center gap-2">
          <Download className="h-4 w-4" /> Export Excel
        </span>
      )}
    </Button>
  );
}

// ─── Payroll-specific controls ────────────────────────────────────────────────
// The payroll report controller accepts month + year (not a date range) and
// the streaming endpoint avoids buffering the full workbook in server RAM.
// Correct endpoint: GET /api/reports/payroll/export/stream?month=M&year=Y

const MONTH_OPTIONS = [
  { value: '1',  label: 'January'   },
  { value: '2',  label: 'February'  },
  { value: '3',  label: 'March'     },
  { value: '4',  label: 'April'     },
  { value: '5',  label: 'May'       },
  { value: '6',  label: 'June'      },
  { value: '7',  label: 'July'      },
  { value: '8',  label: 'August'    },
  { value: '9',  label: 'September' },
  { value: '10', label: 'October'   },
  { value: '11', label: 'November'  },
  { value: '12', label: 'December'  },
];

const CURRENT_YEAR = new Date().getFullYear();
const YEAR_OPTIONS = Array.from({ length: 5 }, (_, i) => CURRENT_YEAR - 2 + i);

function usePayrollPeriod() {
  const now = new Date();
  return useState({ month: String(now.getMonth() + 1), year: String(now.getFullYear()) });
}

/**
 * PayrollExportButton — Fix: calls the server-side streaming endpoint
 * (GET /api/reports/payroll/export/stream) instead of the in-memory
 * ClosedXML export. Streaming uses OpenXmlWriter (O(batch) memory) so
 * it is safe for large payrolls without risk of server OOM.
 */
function PayrollExportButton({
  month, year,
}: { month: string; year: string }) {
  const { toast } = useToast();
  const [loading, setLoading] = useState(false);

  const handleExport = async () => {
    if (!month || !year) {
      toast({ title: 'Select period', description: 'Choose a month and year before exporting.', variant: 'destructive' });
      return;
    }
    setLoading(true);
    try {
      // Use the streaming endpoint — memory-efficient OpenXmlWriter path.
      const url = `${BASE}/api/reports/payroll/export/stream?month=${month}&year=${year}`;
      const res = await csrfFetch(url, { credentials: 'include' });
      if (!res.ok) {
        const text = await res.text().catch(() => `HTTP ${res.status}`);
        throw new Error(text);
      }
      const blob = await res.blob();
      const link = document.createElement('a');
      link.href = URL.createObjectURL(blob);
      link.download = `Payroll_${year}_${month.padStart(2, '0')}.xlsx`;
      link.click();
      URL.revokeObjectURL(link.href);
    } catch (err: unknown) {
      toast({ title: 'Export failed', description: String(err), variant: 'destructive' });
    } finally {
      setLoading(false);
    }
  };

  return (
    <Button variant="outline" size="sm" onClick={handleExport} disabled={loading || !month || !year}>
      {loading ? (
        <span className="flex items-center gap-2">
          <span className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" />
          Exporting…
        </span>
      ) : (
        <span className="flex items-center gap-2">
          <Download className="h-4 w-4" /> Export Excel
        </span>
      )}
    </Button>
  );
}

/**
 * PayrollReportTab — Fix: replaces the generic date-range tab for payroll.
 * Payroll reports are month-scoped (not arbitrary date ranges), matching
 * the PayrollReportController which requires month + year query params.
 * Export uses GET /api/reports/payroll/export/stream (streaming, memory-safe).
 */
function PayrollReportTab() {
  const [period, setPeriod] = usePayrollPeriod();

  return (
    <TabsContent value="payroll" className="mt-4">
      <Card>
        <CardHeader>
          <CardTitle>Payroll Report</CardTitle>
          <CardDescription>
            Payroll cost summary by employee for the selected month.
            Exported via the server-side streaming endpoint — safe for large datasets.
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex flex-wrap items-end gap-4">
            <div className="space-y-1">
              <Label>Month</Label>
              <Select value={period.month} onValueChange={(v) => setPeriod({ ...period, month: v })}>
                <SelectTrigger className="w-36">
                  <SelectValue placeholder="Month" />
                </SelectTrigger>
                <SelectContent>
                  {MONTH_OPTIONS.map((m) => (
                    <SelectItem key={m.value} value={m.value}>{m.label}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1">
              <Label>Year</Label>
              <Select value={period.year} onValueChange={(v) => setPeriod({ ...period, year: v })}>
                <SelectTrigger className="w-28">
                  <SelectValue placeholder="Year" />
                </SelectTrigger>
                <SelectContent>
                  {YEAR_OPTIONS.map((y) => (
                    <SelectItem key={y} value={String(y)}>{y}</SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <PayrollExportButton month={period.month} year={period.year} />
        </CardContent>
      </Card>
    </TabsContent>
  );
}

// ─── Non-payroll tabs (date-range based) ─────────────────────────────────────

const DATE_RANGE_TABS = [
  {
    key: 'attendance', label: 'Attendance',
    description: 'Daily attendance records for all employees in the selected period.',
    endpoint: '/api/reports/attendance',
  },
  {
    key: 'leave', label: 'Leave',
    description: 'Leave requests and balances for the selected period.',
    endpoint: '/api/reports/leave',
  },
  {
    key: 'employee', label: 'Employee',
    description: 'Employee roster and details snapshot.',
    endpoint: '/api/reports/employee',
  },
  {
    key: 'salary', label: 'Salary Register',
    description: 'Salary register with all components for the selected period.',
    endpoint: '/api/reports/salary-register',
  },
];

// Each tab owns its own state — no hook called inside .map()
function ReportTab({ tab }: { tab: typeof DATE_RANGE_TABS[number] }) {
  const [range, setRange] = useDateRange();
  return (
    <TabsContent value={tab.key} className="mt-4">
      <Card>
        <CardHeader>
          <CardTitle>{tab.label} Report</CardTitle>
          <CardDescription>{tab.description}</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <DateRangePicker range={range} onChange={setRange} />
          <ExportButton endpoint={tab.endpoint} label={tab.label} range={range} />
        </CardContent>
      </Card>
    </TabsContent>
  );
}

// ─── All tab keys in display order ────────────────────────────────────────────

const ALL_TABS = [
  { key: 'attendance', label: 'Attendance' },
  { key: 'payroll',    label: 'Payroll'    },
  { key: 'leave',      label: 'Leave'      },
  { key: 'employee',   label: 'Employee'   },
  { key: 'salary',     label: 'Salary Register' },
];

// ─── Page ─────────────────────────────────────────────────────────────────────

export default function ReportsPage() {
  return (
    <div className="space-y-6">
      <PageHeader
        title="Reports"
        description="Export HR data as Excel. Select a period and click Export."
        actions={
          <FileSpreadsheet className="h-6 w-6 text-muted-foreground" />
        }
      />

      <Tabs defaultValue="attendance" className="space-y-4">
        <TabsList className="flex flex-wrap h-auto gap-1 bg-muted p-1 rounded-lg">
          {ALL_TABS.map((t) => (
            <TabsTrigger key={t.key} value={t.key} className="text-sm">
              {t.label}
            </TabsTrigger>
          ))}
        </TabsList>

        {/* Non-payroll tabs use generic date-range export */}
        {DATE_RANGE_TABS.map((tab) => (
          <ReportTab key={tab.key} tab={tab} />
        ))}

        {/* Payroll tab: month/year pickers + streaming endpoint */}
        <PayrollReportTab />
      </Tabs>
    </div>
  );
}
