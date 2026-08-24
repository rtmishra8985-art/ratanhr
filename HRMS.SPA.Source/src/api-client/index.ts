/**
 * @workspace/api-client-react — typed React-Query bindings for the HRMS API.
 *
 * This module replaces the missing workspace package of the same name. Every
 * hook returns a `useQuery`/`useMutation` result whose `data` is typed with the
 * interfaces from `@/types/domain`, so pages get real compile-time checking.
 */

import {
  useMutation,
  useQuery,
  type UseMutationOptions,
  type UseMutationResult,
  type UseQueryOptions,
  type UseQueryResult,
} from '@tanstack/react-query';

import { apiRequest, unwrap, type QueryParams } from './http';
import type { ApiError } from './http';
import type {
  Asset,
  AssetSummary,
  ActivityItem,
  AttendanceRecord,
  AttendanceSummary,
  AuthResponse,
  Candidate,
  DashboardSummary,
  DeptHeadcountItem,
  EmployeeDashboardStats,
  EmployeeDetail,
  EmployeeListItem,
  Goal,
  HelpdeskSummary,
  JobRequisition,
  LeaveRequest,
  LeaveType,
  LoginRequest,
  Notification,
  PagedResult,
  PayrollSummary,
  Payslip,
  PerformanceReview,
  RecruitmentPipeline,
  SalaryStructure,
  Ticket,
  TrendPoint,
  UserProfile,
} from '@/types/domain';

export { setAuthTokenGetter, ApiError, API_BASE_URL } from './http';
export type { QueryParams } from './http';

// ─── Shared option/param shapes ───────────────────────────────────────────────

export interface QueryHookOptions<TData> {
  query?: Partial<UseQueryOptions<TData, ApiError, TData>>;
}

export interface MutationHookOptions<TData, TVariables> {
  mutation?: Partial<UseMutationOptions<TData, ApiError, TVariables>>;
}

export interface PaginationParams extends QueryParams {
  page?: number;
  pageSize?: number;
}

// ─── Normalisers ──────────────────────────────────────────────────────────────

function toArray<T>(payload: unknown): T[] {
  const value = unwrap<unknown>(payload);
  if (Array.isArray(value)) return value as T[];
  if (value && typeof value === 'object') {
    const items = (value as Record<string, unknown>)['items'];
    if (Array.isArray(items)) return items as T[];
  }
  return [];
}

function toPaged<T>(payload: unknown, params?: PaginationParams): PagedResult<T> {
  const value = unwrap<unknown>(payload);
  const page = Number(params?.page ?? 1) || 1;
  const pageSize = Number(params?.pageSize ?? 10) || 10;

  if (Array.isArray(value)) {
    return {
      items: value as T[],
      page,
      pageSize,
      totalCount: value.length,
      totalPages: Math.max(1, Math.ceil(value.length / pageSize)),
    };
  }

  const rec = (value ?? {}) as Record<string, unknown>;
  const items = Array.isArray(rec['items']) ? (rec['items'] as T[]) : [];
  const totalCount = Number(rec['totalCount'] ?? rec['total'] ?? items.length) || 0;
  const resolvedPageSize = Number(rec['pageSize'] ?? pageSize) || pageSize;

  return {
    items,
    page: Number(rec['page'] ?? page) || page,
    pageSize: resolvedPageSize,
    totalCount,
    totalPages:
      Number(rec['totalPages'] ?? Math.ceil(totalCount / resolvedPageSize)) || 1,
  };
}

// ─── Generic hook factories ───────────────────────────────────────────────────

function useApiQuery<TData>(
  defaultQueryKey: readonly unknown[],
  fetcher: (signal: AbortSignal) => Promise<TData>,
  options?: QueryHookOptions<TData>,
): UseQueryResult<TData, ApiError> {
  const { queryKey: overrideKey, ...rest } = options?.query ?? {};
  return useQuery<TData, ApiError, TData>({
    queryFn: ({ signal }) => fetcher(signal),
    ...rest,
    // The caller may override the key, but it must never be undefined.
    queryKey: (overrideKey as readonly unknown[] | undefined) ?? defaultQueryKey,
  });
}


// ─── Query keys ───────────────────────────────────────────────────────────────

export const getGetProfileQueryKey = () => ['/api/profile'] as const;
export const getListEmployeesQueryKey = (params?: PaginationParams) =>
  ['/api/employees', params ?? {}] as const;
export const getGetEmployeeQueryKey = (employeeId?: string) =>
  ['/api/employees', employeeId] as const;
export const getListNotificationsQueryKey = (params?: QueryParams) =>
  ['/api/notifications', params ?? {}] as const;

// ─── Auth & profile ───────────────────────────────────────────────────────────

export function useLogin(
  options?: MutationHookOptions<AuthResponse, { data: LoginRequest }>,
): UseMutationResult<AuthResponse, ApiError, { data: LoginRequest }> {
  return useMutation<AuthResponse, ApiError, { data: LoginRequest }>({
    mutationFn: ({ data }) =>
      apiRequest<unknown>('/api/auth/login', { method: 'POST', body: data }).then(
        (res) => unwrap<AuthResponse>(res),
      ),
    ...(options?.mutation ?? {}),
  });
}

export function useGetProfile(options?: QueryHookOptions<UserProfile>) {
  return useApiQuery<UserProfile>(
    getGetProfileQueryKey(),
    (signal) =>
      apiRequest<unknown>('/api/profile', { signal }).then((r) => unwrap<UserProfile>(r)),
    options,
  );
}

export function useUpdateProfile(
  options?: MutationHookOptions<UserProfile, { data: Partial<UserProfile> }>,
): UseMutationResult<UserProfile, ApiError, { data: Partial<UserProfile> }> {
  return useMutation<UserProfile, ApiError, { data: Partial<UserProfile> }>({
    mutationFn: ({ data }) =>
      apiRequest<unknown>('/api/profile', { method: 'PUT', body: data }).then((r) =>
        unwrap<UserProfile>(r),
      ),
    ...(options?.mutation ?? {}),
  });
}

// ─── Employees ────────────────────────────────────────────────────────────────

export interface ListEmployeesParams extends PaginationParams {
  search?: string;
}

export function useListEmployees(
  params?: ListEmployeesParams,
  options?: QueryHookOptions<PagedResult<EmployeeListItem>>,
) {
  return useApiQuery<PagedResult<EmployeeListItem>>(
    getListEmployeesQueryKey(params),
    (signal) =>
      apiRequest<unknown>('/api/employees', { params, signal }).then((r) =>
        toPaged<EmployeeListItem>(r, params),
      ),
    options,
  );
}

export function useGetEmployee(
  employeeId?: string,
  options?: QueryHookOptions<EmployeeDetail>,
) {
  return useApiQuery<EmployeeDetail>(
    getGetEmployeeQueryKey(employeeId),
    (signal) =>
      apiRequest<unknown>(`/api/employees/${encodeURIComponent(employeeId ?? '')}`, {
        signal,
      }).then((r) => unwrap<EmployeeDetail>(r)),
    options,
  );
}

export function useCreateEmployee(
  options?: MutationHookOptions<EmployeeDetail, { data: FormData | Record<string, unknown> }>,
): UseMutationResult<
  EmployeeDetail,
  ApiError,
  { data: FormData | Record<string, unknown> }
> {
  return useMutation<
    EmployeeDetail,
    ApiError,
    { data: FormData | Record<string, unknown> }
  >({
    mutationFn: ({ data }) =>
      apiRequest<unknown>('/api/employees', { method: 'POST', body: data }).then((r) =>
        unwrap<EmployeeDetail>(r),
      ),
    ...(options?.mutation ?? {}),
  });
}

export function useDeleteEmployee(
  options?: MutationHookOptions<void, { employeeId: string }>,
): UseMutationResult<void, ApiError, { employeeId: string }> {
  return useMutation<void, ApiError, { employeeId: string }>({
    mutationFn: ({ employeeId }) =>
      apiRequest<void>(`/api/employees/${encodeURIComponent(employeeId)}`, {
        method: 'DELETE',
      }),
    ...(options?.mutation ?? {}),
  });
}

// ─── Attendance ───────────────────────────────────────────────────────────────

export function useListAttendance(
  params?: PaginationParams,
  options?: QueryHookOptions<PagedResult<AttendanceRecord>>,
) {
  return useApiQuery<PagedResult<AttendanceRecord>>(
    ['/api/attendance', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/attendance', { params, signal }).then((r) =>
        toPaged<AttendanceRecord>(r, params),
      ),
    options,
  );
}

export function useGetTodayAttendanceSummary(
  options?: QueryHookOptions<AttendanceSummary>,
) {
  return useApiQuery<AttendanceSummary>(
    ['/api/attendance/dashboard'],
    (signal) =>
      apiRequest<unknown>('/api/attendance/dashboard', { signal }).then((r) =>
        unwrap<AttendanceSummary>(r),
      ),
    options,
  );
}

// ─── Leave ────────────────────────────────────────────────────────────────────

export interface ListLeaveRequestsParams extends PaginationParams {
  status?: string;
}

export function useListLeaveRequests(
  params?: ListLeaveRequestsParams,
  options?: QueryHookOptions<PagedResult<LeaveRequest>>,
) {
  return useApiQuery<PagedResult<LeaveRequest>>(
    ['/api/leave', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/leave', { params, signal }).then((r) =>
        toPaged<LeaveRequest>(r, params),
      ),
    options,
  );
}

export function useListLeaveTypes(options?: QueryHookOptions<LeaveType[]>) {
  return useApiQuery<LeaveType[]>(
    ['/api/leave/types'],
    (signal) =>
      apiRequest<unknown>('/api/leave/types', { signal }).then((r) =>
        toArray<LeaveType>(r),
      ),
    options,
  );
}

// ─── Payroll ──────────────────────────────────────────────────────────────────

export function useListPayslips(
  params?: PaginationParams,
  options?: QueryHookOptions<PagedResult<Payslip>>,
) {
  return useApiQuery<PagedResult<Payslip>>(
    ['/api/payslip', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/payslip', { params, signal }).then((r) =>
        toPaged<Payslip>(r, params),
      ),
    options,
  );
}

export function useListSalaryStructures(
  options?: QueryHookOptions<SalaryStructure[]>,
) {
  return useApiQuery<SalaryStructure[]>(
    ['/api/salary'],
    (signal) =>
      apiRequest<unknown>('/api/salary', { signal }).then((r) =>
        toArray<SalaryStructure>(r),
      ),
    options,
  );
}

export function useGetPayrollSummary(
  params?: { year?: number },
  options?: QueryHookOptions<PayrollSummary>,
) {
  return useApiQuery<PayrollSummary>(
    ['/api/analytics/payroll', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/analytics/payroll', { params, signal }).then((r) =>
        unwrap<PayrollSummary>(r),
      ),
    options,
  );
}

// ─── Recruitment ──────────────────────────────────────────────────────────────

export function useListRequisitions(
  params?: PaginationParams,
  options?: QueryHookOptions<PagedResult<JobRequisition>>,
) {
  return useApiQuery<PagedResult<JobRequisition>>(
    ['/api/recruitment/requisitions', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/recruitment/requisitions', { params, signal }).then((r) =>
        toPaged<JobRequisition>(r, params),
      ),
    options,
  );
}

export function useListCandidates(
  params?: PaginationParams,
  options?: QueryHookOptions<PagedResult<Candidate>>,
) {
  return useApiQuery<PagedResult<Candidate>>(
    ['/api/recruitment/candidates', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/recruitment/candidates', { params, signal }).then((r) =>
        toPaged<Candidate>(r, params),
      ),
    options,
  );
}

export function useGetRecruitmentPipeline(
  options?: QueryHookOptions<RecruitmentPipeline>,
) {
  return useApiQuery<RecruitmentPipeline>(
    ['/api/recruitment/dashboard'],
    (signal) =>
      apiRequest<unknown>('/api/recruitment/dashboard', { signal }).then((r) => {
        const value = unwrap<Partial<RecruitmentPipeline>>(r) ?? {};
        return {
          totalCandidates: value.totalCandidates ?? 0,
          totalOpenPositions: value.totalOpenPositions ?? 0,
          stages: value.stages ?? [],
        };
      }),
    options,
  );
}

// ─── Assets ───────────────────────────────────────────────────────────────────

export interface ListAssetsParams extends PaginationParams {
  search?: string;
}

export function useListAssets(
  params?: ListAssetsParams,
  options?: QueryHookOptions<PagedResult<Asset>>,
) {
  return useApiQuery<PagedResult<Asset>>(
    ['/api/assets', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/assets', { params, signal }).then((r) =>
        toPaged<Asset>(r, params),
      ),
    options,
  );
}

export function useGetAssetSummary(options?: QueryHookOptions<AssetSummary>) {
  return useApiQuery<AssetSummary>(
    ['/api/assets/summary'],
    (signal) =>
      apiRequest<unknown>('/api/assets/summary', { signal }).then((r) =>
        unwrap<AssetSummary>(r),
      ),
    options,
  );
}

// ─── Helpdesk ─────────────────────────────────────────────────────────────────

export function useListTickets(
  params?: PaginationParams,
  options?: QueryHookOptions<PagedResult<Ticket>>,
) {
  return useApiQuery<PagedResult<Ticket>>(
    ['/api/helpdesk/tickets', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/helpdesk/tickets', { params, signal }).then((r) =>
        toPaged<Ticket>(r, params),
      ),
    options,
  );
}

export function useGetHelpdeskSummary(options?: QueryHookOptions<HelpdeskSummary>) {
  return useApiQuery<HelpdeskSummary>(
    ['/api/helpdesk/summary'],
    (signal) =>
      apiRequest<unknown>('/api/helpdesk/summary', { signal }).then((r) =>
        unwrap<HelpdeskSummary>(r),
      ),
    options,
  );
}

// ─── Performance ──────────────────────────────────────────────────────────────

export interface PerformanceCycle {
  id: string;
  name: string;
  startDate?: string | null;
  endDate?: string | null;
  status: string;
}

export function useListPerformanceCycles(
  params?: PaginationParams,
  options?: QueryHookOptions<PagedResult<PerformanceCycle>>,
) {
  return useApiQuery<PagedResult<PerformanceCycle>>(
    ['/api/performance/cycles', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/performance/cycles', { params, signal }).then((r) =>
        toPaged<PerformanceCycle>(r, params),
      ),
    options,
  );
}

export function useListGoals(
  params?: PaginationParams,
  options?: QueryHookOptions<PagedResult<Goal>>,
) {
  return useApiQuery<PagedResult<Goal>>(
    ['/api/performance/goals', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/performance/goals', { params, signal }).then((r) =>
        toPaged<Goal>(r, params),
      ),
    options,
  );
}

export function useListReviews(
  params?: PaginationParams,
  options?: QueryHookOptions<PagedResult<PerformanceReview>>,
) {
  return useApiQuery<PagedResult<PerformanceReview>>(
    ['/api/performance/reviews', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/performance/reviews', { params, signal }).then((r) =>
        toPaged<PerformanceReview>(r, params),
      ),
    options,
  );
}

// ─── Notifications ────────────────────────────────────────────────────────────

export function useListNotifications(
  params?: { unreadOnly?: boolean },
  options?: QueryHookOptions<Notification[]>,
) {
  return useApiQuery<Notification[]>(
    getListNotificationsQueryKey(params),
    (signal) =>
      apiRequest<unknown>('/api/notifications', { params, signal }).then((r) =>
        toArray<Notification>(r),
      ),
    options,
  );
}

// ─── Dashboard & analytics ────────────────────────────────────────────────────

export function useGetDashboardSummary(options?: QueryHookOptions<DashboardSummary>) {
  return useApiQuery<DashboardSummary>(
    ['/api/dashboard/admin'],
    (signal) =>
      apiRequest<unknown>('/api/dashboard/admin', { signal }).then((r) =>
        unwrap<DashboardSummary>(r),
      ),
    options,
  );
}

// FIX: employee-facing dashboard stats. Previously the dashboard page called
// only admin-scoped endpoints (/api/dashboard/admin, /api/analytics/*, /api/audit)
// unconditionally for every role, so an Employee session saw four 403s per page
// load instead of a working summary. This hook wires the real employee endpoint
// (which already existed server-side and was unused by the SPA).
export function useGetEmployeeDashboardStats(
  options?: QueryHookOptions<EmployeeDashboardStats>,
) {
  return useApiQuery<EmployeeDashboardStats>(
    ['/api/dashboard/employee'],
    (signal) =>
      apiRequest<unknown>('/api/dashboard/employee', { signal }).then((r) =>
        unwrap<EmployeeDashboardStats>(r),
      ),
    options,
  );
}

export function useGetAttendanceTrend(
  params?: { months?: number },
  options?: QueryHookOptions<TrendPoint[]>,
) {
  return useApiQuery<TrendPoint[]>(
    ['/api/analytics/attendance', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/analytics/attendance', { params, signal }).then((r) =>
        toArray<TrendPoint>(r),
      ),
    options,
  );
}

export function useGetPayrollTrend(
  params?: { months?: number },
  options?: QueryHookOptions<TrendPoint[]>,
) {
  return useApiQuery<TrendPoint[]>(
    ['/api/analytics/payroll-trend', params ?? {}],
    (signal) =>
      apiRequest<unknown>('/api/analytics/payroll', { params, signal }).then((r) =>
        toArray<TrendPoint>(r),
      ),
    options,
  );
}

export function useGetDepartmentHeadcount(
  options?: QueryHookOptions<DeptHeadcountItem[]>,
) {
  return useApiQuery<DeptHeadcountItem[]>(
    ['/api/analytics/headcount'],
    (signal) =>
      apiRequest<unknown>('/api/analytics/headcount', { signal }).then((r) =>
        toArray<DeptHeadcountItem>(r),
      ),
    options,
  );
}

export function useGetRecentActivity(options?: QueryHookOptions<ActivityItem[]>) {
  return useApiQuery<ActivityItem[]>(
    ['/api/audit'],
    (signal) =>
      apiRequest<unknown>('/api/audit', { params: { pageSize: 10 }, signal }).then((r) =>
        toArray<ActivityItem>(r),
      ),
    options,
  );
}
