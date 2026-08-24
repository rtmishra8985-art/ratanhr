/**
 * HRMS API Helper
 * Provides a thin wrapper around the backend REST API.
 * Include this file on every HTML page that needs API access.
 *
 * Auth model: access tokens and refresh tokens are stored exclusively in
 * HttpOnly cookies set by the server. This file never reads or writes tokens —
 * all fetch calls use `credentials: 'include'` so the browser attaches cookies
 * automatically. Only non-sensitive UI metadata (role, name, userId, employeeId,
 * companyId) is stored in localStorage/sessionStorage.
 */

const API_BASE = '/api';

// FIX [3] — Removed getToken() and getRefreshToken(). Tokens live in HttpOnly
// cookies and are never accessible from JavaScript. The browser attaches them
// automatically via credentials: 'include'.

function saveSession(data, remember = false) {
    const store = remember ? localStorage : sessionStorage;
    // FIX [3] — Do NOT store tokens. Only UI metadata (non-sensitive) is persisted.
    if (data.role)       store.setItem('hrms_role',       data.role);
    if (data.fullName)   store.setItem('hrms_name',       data.fullName);
    if (data.userId)     store.setItem('hrms_userId',     String(data.userId));
    if (data.companyId)  store.setItem('hrms_companyId',  String(data.companyId));
    if (data.employeeId) store.setItem('hrms_employeeId', data.employeeId);
    // Seed CSRF cookie immediately after every login / token refresh
    bootstrapCsrf();
}

function clearSession() {
    ['hrms_user','hrms_role','hrms_name',
     'hrms_userId','hrms_companyId','hrms_employeeId'].forEach(k => {
        localStorage.removeItem(k);
        sessionStorage.removeItem(k);
    });
}

function getUser() {
    const raw = localStorage.getItem('hrms_user') || sessionStorage.getItem('hrms_user');
    try { return raw ? JSON.parse(raw) : null; } catch { return null; }
}

// ── Token auto-refresh ─────────────────────────────────────────────────────
let _refreshing = null; // deduplicate concurrent refresh calls

// FIX [3] — tryRefreshToken no longer reads a token from storage. The browser
// sends the HttpOnly refresh-token cookie automatically via credentials:'include'.
// The server accepts the cookie at POST /api/auth/refresh (Path=/api/auth/refresh).
async function tryRefreshToken() {
    try {
        const res = await fetch(API_BASE + '/auth/refresh', {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' }
            // No body — the HttpOnly cookie is sent automatically by the browser.
        });
        if (!res.ok) return false;
        const data = await res.json();
        if (data.success && data.data) {
            // Update non-sensitive UI metadata only (never the token itself).
            const remember = !!localStorage.getItem('hrms_role');
            const store = remember ? localStorage : sessionStorage;
            if (data.data.role)       store.setItem('hrms_role',       data.data.role);
            if (data.data.fullName)   store.setItem('hrms_name',       data.data.fullName);
            return true;
        }
        return false;
    } catch {
        return false;
    }
}

// ── CSRF double-submit header support ──────────────────────────────────────
// Tokens are stored in HttpOnly cookies, so classical CSRF is not possible
// via XSS. The X-XSRF-TOKEN header adds a belt-and-suspenders layer.

const _csrfMethods = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);
let _csrfToken = null;   // cached after first bootstrap

/** Read the XSRF-TOKEN cookie value set by GET /api/auth/csrf */
function _readXsrfCookie() {
    const m = document.cookie.match(/(?:^|;\s*)XSRF-TOKEN=([^;]+)/);
    return m ? decodeURIComponent(m[1]) : null;
}

/** Fetch the CSRF token from the server (once). Idempotent. */
async function bootstrapCsrf() {
    try {
        await fetch(API_BASE + '/auth/csrf', { method: 'GET', credentials: 'include' });
        _csrfToken = _readXsrfCookie();
    } catch { /* non-fatal — token remains null */ }
}

// FIX [3] — apiFetch no longer reads tokens from localStorage or injects
// Authorization headers manually. Cookies are sent automatically via
// credentials: 'include'. The server must accept cookie-based auth.
async function apiFetch(path, options = {}, _retry = true) {
    const headers = { ...options.headers };
    if (!(options.body instanceof FormData)) {
        headers['Content-Type'] = 'application/json';
    }

    // Attach CSRF header on all state-changing requests
    if (_csrfMethods.has((options.method || 'GET').toUpperCase())) {
        if (!_csrfToken) _csrfToken = _readXsrfCookie(); // lazy-read in case cookie already set
        if (_csrfToken) headers['X-XSRF-TOKEN'] = _csrfToken;
    }

    const response = await fetch(API_BASE + path, {
        ...options,
        headers,
        credentials: 'include'   // always send HttpOnly cookies
    });

    // Auto-refresh on 401 then retry once.
    if (response.status === 401 && _retry) {
        if (!_refreshing) _refreshing = tryRefreshToken().finally(() => _refreshing = null);
        const refreshed = await _refreshing;
        if (refreshed) return apiFetch(path, options, false);
        clearSession();
        window.location.replace('/login.html');
        return null;
    }

    // Friendly message for rate-limit hits.
    if (response.status === 429) {
        return { success: false, message: 'Too many requests. Please wait a moment and try again.' };
    }

    if (!response.ok && response.status !== 400) {
        // Non-JSON errors (5xx, 404 etc.) — return a synthetic failure object.
        const text = await response.text().catch(() => '');
        return { success: false, message: text || `Request failed (HTTP ${response.status})` };
    }

    return response.json().catch(() => ({ success: false, message: 'Invalid response from server.' }));
}

// ── Logout helper ──────────────────────────────────────────────────────────
async function logout() {
    try {
        // POST to logout; the server revokes the HttpOnly refresh cookie.
        await fetch(API_BASE + '/auth/logout', {
            method: 'POST',
            credentials: 'include',
            headers: { 'Content-Type': 'application/json' }
        });
    } catch { /* best-effort */ }
    clearSession();
    // Prevent the back button from restoring an authenticated page
    history.pushState(null, '', '/login.html');
    window.addEventListener('popstate', function onPop() {
        window.removeEventListener('popstate', onPop);
        window.location.replace('/login.html');
    });
    window.location.replace('/login.html');
}

const HrmsApi = {
    // Auth
    login: (dto) => apiFetch('/auth/login', { method: 'POST', body: JSON.stringify(dto) }),
    forgotPassword: (email) => apiFetch('/auth/forgot-password', { method: 'POST', body: JSON.stringify({ email }) }),
    resetPassword: (dto) => apiFetch('/auth/reset-password', { method: 'POST', body: JSON.stringify(dto) }),
    changePassword: (dto) => apiFetch('/auth/change-password', { method: 'POST', body: JSON.stringify(dto) }),
    refreshToken: () => apiFetch('/auth/refresh', { method: 'POST' }),
    logout: () => logout(),

    // Leave Management
    listLeaveTypes: () => apiFetch('/leave/types'),
    createLeaveType: (dto) => apiFetch('/leave/types', { method: 'POST', body: JSON.stringify(dto) }),
    applyLeave: (dto) => apiFetch('/leave/apply', { method: 'POST', body: JSON.stringify(dto) }),
    myLeaveRequests: () => apiFetch('/leave/my'),
    myLeaveBalance: () => apiFetch('/leave/my/balance'),
    cancelLeave: (id) => apiFetch(`/leave/my/${id}/cancel`, { method: 'POST' }),
    listAllLeaveRequests: (status) => apiFetch('/leave' + (status ? `?status=${encodeURIComponent(status)}` : '')),
    decideLeave: (id, dto) => apiFetch(`/leave/${id}/decision`, { method: 'POST', body: JSON.stringify(dto) }),

    // Dashboard
    adminDashboard: () => apiFetch('/dashboard/admin'),
    superAdminDashboard: () => apiFetch('/dashboard/superadmin'),
    employeeDashboard: () => apiFetch('/dashboard/employee'),

    // Employees
    createEmployee: (formData) => apiFetch('/employees', { method: 'POST', body: formData }),
    listEmployees: () => apiFetch('/employees'),
    getEmployee: (id) => apiFetch(`/employees/${id}`),
    updateEmployee: (id, formData) => apiFetch(`/employees/${id}`, { method: 'PUT', body: formData }),
    toggleEmployeeStatus: (id, isActive) => apiFetch(`/employees/${id}/status`, { method: 'PATCH', body: JSON.stringify({ isActive }) }),
    deleteEmployee: (id) => apiFetch(`/employees/${id}`, { method: 'DELETE' }),
    myProfile: () => apiFetch('/my/profile'),
    updateMyProfile: (formData) => apiFetch('/my/profile', { method: 'PUT', body: formData }),

    // Companies
    createCompany: (dto) => apiFetch('/companies', { method: 'POST', body: JSON.stringify(dto) }),
    listCompanies: () => apiFetch('/companies'),
    getCompany: (id) => apiFetch(`/companies/${id}`),
    updateCompany: (id, dto) => apiFetch(`/companies/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    uploadLogo: (id, formData) => apiFetch(`/companies/${id}/logo`, { method: 'POST', body: formData }),
    deleteCompany: (id) => apiFetch(`/companies/${id}`, { method: 'DELETE' }),

    // Attendance – Web
    checkIn: () => apiFetch('/attendance/web/check-in', { method: 'POST' }),
    checkOut: (id) => apiFetch(`/attendance/web/check-out/${id}`, { method: 'POST' }),
    getWebAttendance: (params = {}) => apiFetch('/attendance/web?' + new URLSearchParams(params)),
    getMyAttendance: (params = {}) => apiFetch('/attendance/web/my?' + new URLSearchParams(params)),
    updateAttendanceStatus: (id, status) => apiFetch(`/attendance/web/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
    editAttendance: (id, dto) => apiFetch(`/attendance/web/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),

    // Attendance – Excel
    uploadExcelAttendance: (formData) => apiFetch('/attendance/excel/upload', { method: 'POST', body: formData }),
    getExcelAttendance: (params = {}) => apiFetch('/attendance/excel?' + new URLSearchParams(params)),

    // Payroll
    generatePayslip: (dto) => apiFetch('/payroll/generate', { method: 'POST', body: JSON.stringify(dto) }),
    listPayslips: (params = {}) => apiFetch('/payroll?' + new URLSearchParams(params)),
    getPayslip: (id) => apiFetch(`/payroll/${id}`),
    myPayslips: () => apiFetch('/payroll/my'),
    deletePayslip: (id) => apiFetch(`/payroll/${id}`, { method: 'DELETE' }),
    bulkGeneratePayroll: (dto) => apiFetch('/payroll/bulk', { method: 'POST', body: JSON.stringify(dto) }),

    // Reports
    attendanceReport: (params = {}) => apiFetch('/reports/attendance?' + new URLSearchParams(params)),
    employeeReport: (params = {}) => apiFetch('/reports/employees?' + new URLSearchParams(params)),
    payrollReport: (params = {}) => apiFetch('/reports/payroll?' + new URLSearchParams(params)),
    leaveReport: (params = {}) => apiFetch('/reports/leave?' + new URLSearchParams(params)),
    salaryRegisterReport: (params = {}) => apiFetch('/reports/salary-register?' + new URLSearchParams(params)),

    // Appreciation
    uploadAppreciation: (formData) => apiFetch('/appreciation', { method: 'POST', body: formData }),
    listAppreciations: () => apiFetch('/appreciation'),
    myAppreciations: () => apiFetch('/appreciation/my'),

    // Admin Users
    listAdminUsers: () => apiFetch('/admin-users'),
    createAdminUser: (dto) => apiFetch('/admin-users', { method: 'POST', body: JSON.stringify(dto) }),
    toggleAdminStatus: (id, isActive) => apiFetch(`/admin-users/${id}/status`, { method: 'PATCH', body: JSON.stringify({ isActive }) }),
    deleteAdminUser: (id) => apiFetch(`/admin-users/${id}`, { method: 'DELETE' }),

    // Permissions
    listPermissions: () => apiFetch('/permissions'),
    getPermission: (role) => apiFetch(`/permissions/${role}`),
    savePermissions: (dto) => apiFetch('/permissions', { method: 'POST', body: JSON.stringify(dto) }),

    // SuperAdmins
    listSuperAdmins: () => apiFetch('/superadmins'),
    createSuperAdmin: (dto) => apiFetch('/superadmins', { method: 'POST', body: JSON.stringify(dto) }),
    toggleSuperAdminStatus: (id, isActive) => apiFetch(`/superadmins/${id}/status`, { method: 'PATCH', body: JSON.stringify({ isActive }) }),

    // ── Recruitment ──────────────────────────────────────────────────────
    recruitmentDashboard: () => apiFetch('/recruitment/dashboard'),
    // ── Mini CRM (Sales) ──────────────────────────────────────────────────
    salesDashboard: () => apiFetch('/sales/dashboard'),

    listLeads: (page=1, pageSize=20, status=null, search=null) => {
        const p = new URLSearchParams({ page, pageSize });
        if (status) p.set('status', status);
        if (search) p.set('search', search);
        return apiFetch('/sales/leads?' + p);
    },
    getLead:          (id) => apiFetch(`/sales/leads/${id}`),
    createLead:       (dto) => apiFetch('/sales/leads', { method: 'POST', body: JSON.stringify(dto) }),
    updateLead:       (id, dto) => apiFetch(`/sales/leads/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    updateLeadStatus: (id, status) => apiFetch(`/sales/leads/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
    deleteLead:       (id) => apiFetch(`/sales/leads/${id}`, { method: 'DELETE' }),
    convertLead:      (leadId, dto) => apiFetch(`/sales/leads/${leadId}/convert`, { method: 'POST', body: JSON.stringify(dto) }),

    listCustomers: (page=1, pageSize=20, search=null) => {
        const p = new URLSearchParams({ page, pageSize });
        if (search) p.set('search', search);
        return apiFetch('/sales/customers?' + p);
    },
    getCustomer:    (id) => apiFetch(`/sales/customers/${id}`),
    createCustomer: (dto) => apiFetch('/sales/customers', { method: 'POST', body: JSON.stringify(dto) }),
    updateCustomer: (id, dto) => apiFetch(`/sales/customers/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    deleteCustomer: (id) => apiFetch(`/sales/customers/${id}`, { method: 'DELETE' }),

    listFollowUps:   (leadId=null, status=null) => {
        const p = new URLSearchParams();
        if (leadId) p.set('leadId', leadId);
        if (status) p.set('status', status);
        return apiFetch('/sales/followups?' + p);
    },
    createFollowUp: (dto) => apiFetch('/sales/followups', { method: 'POST', body: JSON.stringify(dto) }),
    updateFollowUp: (id, dto) => apiFetch(`/sales/followups/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    deleteFollowUp: (id) => apiFetch(`/sales/followups/${id}`, { method: 'DELETE' }),

    listMeetings:   (leadId=null, customerId=null) => {
        const p = new URLSearchParams();
        if (leadId)     p.set('leadId', leadId);
        if (customerId) p.set('customerId', customerId);
        return apiFetch('/sales/meetings?' + p);
    },
    getMeeting:    (id) => apiFetch(`/sales/meetings/${id}`),
    createMeeting: (dto) => apiFetch('/sales/meetings', { method: 'POST', body: JSON.stringify(dto) }),
    updateMeeting: (id, dto) => apiFetch(`/sales/meetings/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    deleteMeeting: (id) => apiFetch(`/sales/meetings/${id}`, { method: 'DELETE' }),

    listVisits:  (leadId=null, customerId=null) => {
        const p = new URLSearchParams();
        if (leadId)     p.set('leadId', leadId);
        if (customerId) p.set('customerId', customerId);
        return apiFetch('/sales/visits?' + p);
    },
    salesCheckIn:  (dto) => apiFetch('/sales/visits/checkin', { method: 'POST', body: JSON.stringify(dto) }),
    salesCheckOut: (id, dto) => apiFetch(`/sales/visits/${id}/checkout`, { method: 'PATCH', body: JSON.stringify(dto) }),
    deleteVisit:   (id) => apiFetch(`/sales/visits/${id}`, { method: 'DELETE' }),

    listSalesTasks: (leadId=null, customerId=null, status=null) => {
        const p = new URLSearchParams();
        if (leadId)     p.set('leadId', leadId);
        if (customerId) p.set('customerId', customerId);
        if (status)     p.set('status', status);
        return apiFetch('/sales/tasks?' + p);
    },
    createSalesTask:       (dto) => apiFetch('/sales/tasks', { method: 'POST', body: JSON.stringify(dto) }),
    updateSalesTask:       (id, dto) => apiFetch(`/sales/tasks/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    updateSalesTaskStatus: (id, status) => apiFetch(`/sales/tasks/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
    deleteSalesTask:       (id) => apiFetch(`/sales/tasks/${id}`, { method: 'DELETE' }),

    listQuotations: (leadId=null, customerId=null) => {
        const p = new URLSearchParams();
        if (leadId)     p.set('leadId', leadId);
        if (customerId) p.set('customerId', customerId);
        return apiFetch('/sales/quotations?' + p);
    },
    getQuotation:           (id) => apiFetch(`/sales/quotations/${id}`),
    createQuotation:        (dto) => apiFetch('/sales/quotations', { method: 'POST', body: JSON.stringify(dto) }),
    updateQuotation:        (id, dto) => apiFetch(`/sales/quotations/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    updateQuotationStatus:  (id, status) => apiFetch(`/sales/quotations/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
    deleteQuotation:        (id) => apiFetch(`/sales/quotations/${id}`, { method: 'DELETE' }),

    salesLeadReport:       (from=null, to=null) => apiFetch('/sales/reports/leads'      + (from||to ? '?from='+(from||'')+'&to='+(to||'') : '')),
    salesConversionReport: (from=null, to=null) => apiFetch('/sales/reports/conversion' + (from||to ? '?from='+(from||'')+'&to='+(to||'') : '')),
    salesPerfReport:       (from=null, to=null) => apiFetch('/sales/reports/performance'+ (from||to ? '?from='+(from||'')+'&to='+(to||'') : '')),
    salesVisitReport:      (from=null, to=null) => apiFetch('/sales/reports/visits'     + (from||to ? '?from='+(from||'')+'&to='+(to||'') : '')),
    salesRevenueReport:    (from=null, to=null) => apiFetch('/sales/reports/revenue'    + (from||to ? '?from='+(from||'')+'&to='+(to||'') : '')),
    salesPipelineReport:   ()                   => apiFetch('/sales/reports/pipeline'),

    // ── Lead Assignment ───────────────────────────────────────────────────
    assignLead:      (id, dto) => apiFetch(`/sales/leads/${id}/assign`, { method: 'POST', body: JSON.stringify(dto) }),
    reassignLead:    (id, dto) => apiFetch(`/sales/leads/${id}/reassign`, { method: 'POST', body: JSON.stringify(dto) }),
    bulkAssignLeads: (dto) => apiFetch('/sales/leads/bulk-assign', { method: 'POST', body: JSON.stringify(dto) }),
    leadAssignmentHistory: (id) => apiFetch(`/sales/leads/${id}/assignment-history`),
    myAssignedLeads:  (empId, page=1, pageSize=20) => apiFetch(`/sales/leads/my-leads?employeeId=${empId}&page=${page}&pageSize=${pageSize}`),
    unassignedLeads:  (page=1, pageSize=20) => apiFetch(`/sales/leads/unassigned?page=${page}&pageSize=${pageSize}`),
    teamLeads:        (managerId, page=1, pageSize=20) => apiFetch(`/sales/leads/team-leads?managerId=${managerId}&page=${page}&pageSize=${pageSize}`),

    // ── Recruitment ───────────────────────────────────────────────────────
    listRequisitions: (params = {}) => apiFetch('/recruitment/requisitions?' + new URLSearchParams(params)),
    createRequisition: (dto) => apiFetch('/recruitment/requisitions', { method: 'POST', body: JSON.stringify(dto) }),
    getRequisition: (id) => apiFetch(`/recruitment/requisitions/${id}`),
    updateRequisition: (id, dto) => apiFetch(`/recruitment/requisitions/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    updateRequisitionStatus: (id, status) => apiFetch(`/recruitment/requisitions/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
    deleteRequisition: (id) => apiFetch(`/recruitment/requisitions/${id}`, { method: 'DELETE' }),

    listCandidates: (params = {}) => apiFetch('/recruitment/candidates?' + new URLSearchParams(params)),
    createCandidate: (formData) => apiFetch('/recruitment/candidates', { method: 'POST', body: formData }),
    getCandidate: (id) => apiFetch(`/recruitment/candidates/${id}`),
    updateCandidate: (id, formData) => apiFetch(`/recruitment/candidates/${id}`, { method: 'PUT', body: formData }),
    updateCandidateStatus: (id, status, notes) => apiFetch(`/recruitment/candidates/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status, notes }) }),
    deleteCandidate: (id) => apiFetch(`/recruitment/candidates/${id}`, { method: 'DELETE' }),

    listInterviews: (params = {}) => apiFetch('/recruitment/interviews?' + new URLSearchParams(params)),
    scheduleInterview: (dto) => apiFetch('/recruitment/interviews', { method: 'POST', body: JSON.stringify(dto) }),
    updateInterview: (id, dto) => apiFetch(`/recruitment/interviews/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    submitInterviewFeedback: (id, dto) => apiFetch(`/recruitment/interviews/${id}/feedback`, { method: 'POST', body: JSON.stringify(dto) }),
    deleteInterview: (id) => apiFetch(`/recruitment/interviews/${id}`, { method: 'DELETE' }),

    listOffers: (params = {}) => apiFetch('/recruitment/offers?' + new URLSearchParams(params)),
    createOffer: (dto) => apiFetch('/recruitment/offers', { method: 'POST', body: JSON.stringify(dto) }),
    getOffer: (id) => apiFetch(`/recruitment/offers/${id}`),
    approveOffer: (id, dto) => apiFetch(`/recruitment/offers/${id}/approve`, { method: 'POST', body: JSON.stringify(dto) }),
    updateOfferStatus: (id, status) => apiFetch(`/recruitment/offers/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),

    // ── Performance Management ───────────────────────────────────────────
    performanceDashboard: () => apiFetch('/performance/dashboard'),

    listPerformanceCycles: (params = {}) => apiFetch('/performance/cycles?' + new URLSearchParams(params)),
    createPerformanceCycle: (dto) => apiFetch('/performance/cycles', { method: 'POST', body: JSON.stringify(dto) }),
    updatePerformanceCycle: (id, dto) => apiFetch(`/performance/cycles/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    deletePerformanceCycle: (id) => apiFetch(`/performance/cycles/${id}`, { method: 'DELETE' }),

    listGoals: (params = {}) => apiFetch('/performance/goals?' + new URLSearchParams(params)),
    createGoal: (dto) => apiFetch('/performance/goals', { method: 'POST', body: JSON.stringify(dto) }),
    updateGoal: (id, dto) => apiFetch(`/performance/goals/${id}`, { method: 'PUT', body: JSON.stringify(dto) }),
    updateGoalProgress: (id, achievedValue) => apiFetch(`/performance/goals/${id}/progress`, { method: 'PATCH', body: JSON.stringify({ achievedValue }) }),
    deleteGoal: (id) => apiFetch(`/performance/goals/${id}`, { method: 'DELETE' }),

    listReviews: (params = {}) => apiFetch('/performance/reviews?' + new URLSearchParams(params)),
    createReview: (dto) => apiFetch('/performance/reviews', { method: 'POST', body: JSON.stringify(dto) }),
    getReview: (id) => apiFetch(`/performance/reviews/${id}`),
    submitSelfReview: (id, dto) => apiFetch(`/performance/reviews/${id}/self`, { method: 'POST', body: JSON.stringify(dto) }),
    submitManagerReview: (id, dto) => apiFetch(`/performance/reviews/${id}/manager`, { method: 'POST', body: JSON.stringify(dto) }),
    finalizeReview: (id, dto) => apiFetch(`/performance/reviews/${id}/finalize`, { method: 'POST', body: JSON.stringify(dto) }),

    listFeedback: (params = {}) => apiFetch('/performance/feedback?' + new URLSearchParams(params)),
    submitFeedback: (dto) => apiFetch('/performance/feedback', { method: 'POST', body: JSON.stringify(dto) }),
};

// FIX [3] — requireAuth no longer checks for a token in storage (tokens are
// in HttpOnly cookies and cannot be read from JS). It checks for the role
// metadata set on login; actual auth enforcement is done by the server.
// Any unauthenticated request returns 401, which apiFetch handles by
// redirecting to /login.html.
function requireAuth(allowedRoles = []) {
    const role = localStorage.getItem('hrms_role') || sessionStorage.getItem('hrms_role');
    if (!role) {
        window.location.replace('/login.html');
        return false;
    }
    if (allowedRoles.length > 0 && !allowedRoles.includes(role)) {
        window.location.replace('/access-denied.html');
        return false;
    }
    return true;
}

// Populate topnav user info from localStorage
function populateTopnav() {
    const name  = localStorage.getItem('hrms_name')       || sessionStorage.getItem('hrms_name')       || '';
    const role  = localStorage.getItem('hrms_role')       || sessionStorage.getItem('hrms_role')       || '';
    const empId = localStorage.getItem('hrms_employeeId') || sessionStorage.getItem('hrms_employeeId') || '';
    const el       = document.getElementById('navUserName');
    const elRole   = document.getElementById('navUserRole');
    const elAvatar = document.getElementById('navAvatar');
    if (el)       el.textContent = name;
    if (elRole)   elRole.textContent = role ? (role.charAt(0).toUpperCase() + role.slice(1)) + (empId ? ` · ${empId}` : '') : '';
    if (elAvatar) elAvatar.textContent = name ? name.split(' ').map(w => w[0]).join('').substring(0, 2).toUpperCase() : '?';
}
