/**
 * domain.ts — Explicit TypeScript interfaces for every HRMS domain object.
 *
 * These types describe the shapes returned by @workspace/api-client-react hooks.
 * Using explicit interfaces instead of inferred API types gives:
 *  - Better IDE autocomplete across the whole frontend.
 *  - Compile-time errors when API shapes change.
 *  - A single source of truth for all domain models.
 */

// ─── Auth ─────────────────────────────────────────────────────────────────────

export type LoginPortal = 'employee' | 'admin' | 'superadmin';

export interface LoginRequest {
  email: string;
  password: string;
  /**
   * FIX: the backend's LoginDto.Portal defaults to "employee" and
   * AuthService strictly enforces portal-role matching (a request whose
   * Portal does not match the account's Role is rejected with 401, even
   * with correct credentials). Previously this field did not exist on the
   * frontend type or the login form, so SuperAdmin and Admin accounts could
   * never authenticate through the UI — every request silently defaulted
   * to the "employee" portal server-side.
   */
  portal: LoginPortal;
}

export interface AuthResponse {
  /** The API may include this for compatibility; authentication is cookie-based. */
  token?: string;
  accessToken?: string;
  refreshToken?: string;
  expiresIn?: number;
  /** Matches HRMS.Application.DTOs.Auth.LoginResponseDto (backend). */
  role?: string;
  fullName?: string;
  userId?: number;
  companyId?: number | null;
  employeeId?: string | null;
  mustChangePassword?: boolean;
  expiresAt?: string;
  mfaRequired?: boolean;
  tempToken?: string | null;
}

// ─── Profile ──────────────────────────────────────────────────────────────────

export interface UserProfile {
  id?: string | null;
  employeeId?: string | null;
  fullName?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
  role?: string | null;
  designation?: string | null;
  departmentName?: string | null;
  avatarUrl?: string | null;
  phone?: string | null;
  companyName?: string | null;
  branchName?: string | null;
}

// ─── Paginated response ───────────────────────────────────────────────────────

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

// ─── Employees ────────────────────────────────────────────────────────────────

export interface EmployeeListItem {
  employeeId: string;
  firstName?: string | null;
  lastName?: string | null;
  email?: string | null;
  avatarUrl?: string | null;
  departmentName?: string | null;
  designation?: string | null;
  status: string;
}

export interface EmployeeDetail extends EmployeeListItem {
  phone?: string | null;
  gender?: string | null;
  dateOfBirth?: string | null;
  joinDate?: string | null;
  managerId?: string | null;
  managerName?: string | null;
  address?: string | null;
  emergencyContactName?: string | null;
  emergencyContactPhone?: string | null;
  documents?: EmployeeDocument[] | null;
  leaves?: LeaveBalance[] | null;
}

export interface EmployeeDocument {
  id: string;
  name: string;
  type?: string | null;
  url?: string | null;
  uploadedAt?: string | null;
}

// ─── Attendance ───────────────────────────────────────────────────────────────

export interface AttendanceRecord {
  id: string;
  employeeName?: string | null;
  employeeId?: string | null;
  date: string;
  checkIn?: string | null;
  checkOut?: string | null;
  workHours?: number | null;
  status: string;
}

export interface AttendanceSummary {
  total: number;
  present: number;
  late: number;
  absent: number;
  onLeave: number;
}

// ─── Leave ────────────────────────────────────────────────────────────────────

export interface LeaveRequest {
  id: string;
  employeeId?: string | null;
  employeeName?: string | null;
  leaveTypeName?: string | null;
  startDate: string;
  endDate: string;
  days: number;
  reason?: string | null;
  status: string;
  appliedAt?: string | null;
}

export interface LeaveType {
  id: string;
  name: string;
  isPaid: boolean;
  maxDays?: number | null;
}

export interface LeaveBalance {
  leaveTypeId: string;
  leaveTypeName: string;
  allocated: number;
  used: number;
  remaining: number;
}

// ─── Payroll ──────────────────────────────────────────────────────────────────

export interface Payslip {
  id: string;
  employeeId?: string | null;
  employeeName?: string | null;
  month: string;
  year: number;
  grossSalary?: number | null;
  deductions?: number | null;
  netSalary?: number | null;
  status: string;
}

export interface SalaryStructure {
  id: string;
  name: string;
  basicSalary?: number | null;
  hra?: number | null;
  allowances?: number | null;
}

export interface PayrollSummaryMonth {
  month: string;
  totalGross: number;
  totalNet: number;
}

export interface PayrollSummary {
  year: number;
  months: PayrollSummaryMonth[];
}

// ─── Recruitment ──────────────────────────────────────────────────────────────

export interface JobRequisition {
  id: string;
  jobTitle: string;
  departmentName?: string | null;
  openings: number;
  candidateCount?: number | null;
  status: string;
}

export interface Candidate {
  id: string;
  fullName: string;
  email?: string | null;
  position?: string | null;
  appliedAt?: string | null;
  rating?: number | null;
  status: string;
}

export interface RecruitmentPipelineStage {
  stage: string;
  count: number;
}

export interface RecruitmentPipeline {
  totalCandidates: number;
  totalOpenPositions: number;
  stages: RecruitmentPipelineStage[];
}

// ─── Assets ───────────────────────────────────────────────────────────────────

export interface Asset {
  id: string;
  assetCode: string;
  name: string;
  categoryName?: string | null;
  assignedToName?: string | null;
  status: string;
}

export interface AssetSummary {
  total: number;
  assigned: number;
  available: number;
  underMaintenance: number;
}

// ─── Helpdesk ─────────────────────────────────────────────────────────────────

export interface Ticket {
  id: number;
  title: string;
  categoryName?: string | null;
  raisedByName?: string | null;
  priority: string;
  status: string;
  createdAt?: string | null;
}

export interface HelpdeskSummary {
  open: number;
  inProgress: number;
  resolved: number;
  critical: number;
}

// ─── Performance ──────────────────────────────────────────────────────────────

export interface Goal {
  id: string;
  title: string;
  employeeName?: string | null;
  progress?: number | null;
  dueDate?: string | null;
  status: string;
}

export interface PerformanceReview {
  id: string;
  employeeName?: string | null;
  cycleName?: string | null;
  selfRating?: number | null;
  managerRating?: number | null;
  finalRating?: number | null;
  status: string;
}

// ─── Dashboard ────────────────────────────────────────────────────────────────

export interface DashboardSummary {
  totalEmployees?: number | null;
  presentToday?: number | null;
  onLeave?: number | null;
  openPositions?: number | null;
  pendingLeaves?: number | null;
  openTickets?: number | null;
  totalAssets?: number | null;
  monthlyPayroll?: number | null;
}

/** Matches HRMS.Application.DTOs.Report.EmployeeDashboardStatsDto (backend). */
export interface EmployeeDashboardStats {
  employeeId?: string | null;
  fullName?: string | null;
  pendingLeaves: number;
  approvedLeavesThisMonth: number;
  totalLeavesUsedThisYear: number;
  checkedInToday: boolean;
  todayCheckInTime?: string | null;
  todayCheckOutTime?: string | null;
  hoursWorkedToday?: number | null;
  attendanceDaysThisMonth: number;
  workingDaysThisMonth: number;
  lastNetPay?: number | null;
  lastPayMonth?: string | null;
  upcomingHolidays: number;
}

export interface TrendPoint {
  label: string;
  value: number;
  secondaryValue?: number | null;
}

export interface DeptHeadcountItem {
  department: string;
  count: number;
}

export interface ActivityItem {
  id: string;
  message: string;
  timestamp: string;
  actorName?: string | null;
}

// ─── Notifications ────────────────────────────────────────────────────────────

export interface Notification {
  id: string;
  message: string;
  isRead: boolean;
  createdAt: string;
}
