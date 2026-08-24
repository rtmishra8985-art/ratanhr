import { Link, useLocation } from 'wouter';
import { useAuth } from '@/hooks/useAuth';
import { useGetProfile, getGetProfileQueryKey } from '@workspace/api-client-react';
import {
  LayoutDashboard,
  Users,
  CalendarCheck,
  CalendarDays,
  Banknote,
  Briefcase,
  TrendingUp,
  MonitorSmartphone,
  LifeBuoy,
  Settings,
  LogOut,
  FileBarChart2,
  GitBranch,
  GraduationCap,
  Receipt,
  Plane,
  ClipboardList,
  Clock,
  Building2,
  Fingerprint,
  Cpu,
  BarChart3,
  DollarSign,
  Shield,
  ShoppingBag,
  Webhook,
  type LucideIcon,
} from 'lucide-react';
import {
  Sidebar as SidebarUI,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from '@/components/ui/sidebar';
import { getUserInitials, getDisplayName, getRole } from '@/utils/profileHelpers';

const coreNav = [
  { name: 'Dashboard',            href: '/dashboard',                  icon: LayoutDashboard },
  { name: 'Employees',            href: '/employees',                  icon: Users },
  { name: 'Attendance',           href: '/attendance',                 icon: CalendarCheck },
  { name: 'Timesheet',            href: '/timesheet',                  icon: Clock },
  { name: 'Leave',                href: '/leave',                      icon: CalendarDays },
  { name: 'Payroll',              href: '/payroll',                    icon: Banknote },
  // Fix M-05: Bonuses & Deductions sub-page of Payroll
  { name: 'Bonuses & Deductions', href: '/payroll/bonuses-deductions', icon: DollarSign },
  { name: 'Recruitment',          href: '/recruitment',                icon: Briefcase },
  { name: 'Performance',          href: '/performance',                icon: TrendingUp },
  { name: 'Assets',               href: '/assets',                     icon: MonitorSmartphone },
  { name: 'Helpdesk',             href: '/helpdesk',                   icon: LifeBuoy },
];

const modulesNav = [
  { name: 'Training',   href: '/training',   icon: GraduationCap },
  { name: 'Expenses',   href: '/expenses',   icon: Receipt },
  { name: 'Travel',     href: '/travel',     icon: Plane },
  { name: 'Onboarding', href: '/onboarding', icon: ClipboardList },
  // Fix: Sales/CRM nav item — was entirely missing despite full backend + frontend implementation
  { name: 'Sales / CRM', href: '/sales',     icon: ShoppingBag },
];

// Restored: Organisation management pages (Shift, Biometric, Department, Holiday)
const orgNav = [
  { name: 'Departments',       href: '/departments',       icon: Building2 },
  { name: 'Designations',      href: '/designations',      icon: Briefcase },
  { name: 'Shifts',            href: '/shifts',            icon: Clock },
  { name: 'Holidays',          href: '/holidays',          icon: CalendarDays },
  { name: 'Biometric Logs',    href: '/biometric',         icon: Fingerprint },
  { name: 'Biometric Devices', href: '/biometric/devices', icon: Cpu },
];

const toolsNav = [
  { name: 'Analytics', href: '/analytics', icon: BarChart3 },
  { name: 'Reports',   href: '/reports',   icon: FileBarChart2 },
  { name: 'Org Chart', href: '/org-chart', icon: GitBranch },
  { name: 'Webhooks',  href: '/webhooks', icon: Webhook },
  // Fix L-01: Audit Log viewer
  { name: 'Audit Log', href: '/audit-log', icon: Shield },
  { name: 'Settings',  href: '/settings',  icon: Settings },
];

export function Sidebar() {
  const [location] = useLocation();
  const { logout } = useAuth();
  const { data: profile } = useGetProfile({
    query: { enabled: true, queryKey: getGetProfileQueryKey() },
  });

  const isActive = (href: string) => {
    // Exact match for dashboard to avoid it being active on all pages
    if (href === '/dashboard') return location === '/dashboard' || location === '/';
    // More-specific routes must win: /biometric/devices must not also activate /biometric
    // and /payroll/bonuses-deductions must not also activate /payroll
    return location === href || location.startsWith(href + '/');
  };

  const NavGroup = ({
    items,
    label,
  }: {
    items: Array<{ name: string; href: string; icon: LucideIcon; external?: boolean }>;
    label?: string;
  }) => (
    <SidebarGroup>
      {label && <SidebarGroupLabel>{label}</SidebarGroupLabel>}
      <SidebarGroupContent>
        <SidebarMenu>
          {items.map((item) => (
            <SidebarMenuItem key={item.name}>
              <SidebarMenuButton
                asChild
                isActive={isActive(item.href)}
                tooltip={item.name}
              >
                {item.external ? (
                  <a href={item.href} className="flex items-center gap-3 w-full">
                    <item.icon className="h-4 w-4" />
                    <span>{item.name}</span>
                  </a>
                ) : (
                  <Link href={item.href} className="flex items-center gap-3 w-full">
                    <item.icon className="h-4 w-4" />
                    <span>{item.name}</span>
                  </Link>
                )}
              </SidebarMenuButton>
            </SidebarMenuItem>
          ))}
        </SidebarMenu>
      </SidebarGroupContent>
    </SidebarGroup>
  );

  return (
    <SidebarUI variant="sidebar" className="border-r border-border/50">
      <SidebarHeader className="h-16 flex items-center px-4 border-b border-sidebar-border">
        <div className="flex items-center gap-2 px-2 w-full">
          <div className="h-8 w-8 rounded-md bg-primary flex items-center justify-center">
            <span className="text-primary-foreground font-bold text-lg">H</span>
          </div>
          <span className="font-semibold text-lg tracking-tight truncate flex-1">HRMS Pro</span>
        </div>
      </SidebarHeader>

      <SidebarContent className="py-2 overflow-y-auto">
        <NavGroup items={coreNav} />
        <NavGroup items={modulesNav} label="Modules" />
        <NavGroup items={orgNav}     label="Organisation" />
        <NavGroup items={toolsNav}   label="Tools" />
      </SidebarContent>

      <SidebarFooter className="p-4 border-t border-sidebar-border">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3 overflow-hidden">
            <div className="h-8 w-8 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
              <span className="text-primary text-xs font-semibold">
                {getUserInitials(profile)}
              </span>
            </div>
            <div className="flex flex-col overflow-hidden">
              <span className="text-sm font-medium truncate">{getDisplayName(profile)}</span>
              <span className="text-xs text-muted-foreground truncate">{getRole(profile)}</span>
            </div>
          </div>
          {/* Fix #10: aria-label added so screen readers announce the button purpose */}
          <button
            onClick={logout}
            className="text-muted-foreground hover:text-foreground p-2 rounded-md hover:bg-sidebar-accent transition-colors shrink-0"
            title="Log out"
            aria-label="Log out"
          >
            <LogOut className="h-4 w-4" />
          </button>
        </div>
      </SidebarFooter>
    </SidebarUI>
  );
}
