import { useMemo } from 'react';
import { Tree, TreeNode } from 'react-organizational-chart';
import { useListEmployees } from '@workspace/api-client-react';
import { PageHeader } from '@/components/layout/PageHeader';
import { Skeleton } from '@/components/ui/skeleton';
import { SafeAvatar } from '@/components/shared/SafeAvatar';
import { EmptyState } from '@/components/shared/EmptyState';

interface EmpNode {
  employeeId: string;
  firstName?: string | null;
  lastName?: string | null;
  designation?: string | null;
  departmentName?: string | null;
  avatarUrl?: string | null;
  managerId?: string | null;
  children: EmpNode[];
}

function EmployeeCard({ node }: { node: EmpNode }) {
  const name = [node.firstName, node.lastName].filter(Boolean).join(' ') || '—';
  return (
    <div className="inline-flex flex-col items-center gap-1 px-4 py-3 bg-card border rounded-xl shadow-sm min-w-[130px] max-w-[180px] hover:shadow-md transition-shadow">
      <SafeAvatar
        profile={{ firstName: node.firstName, lastName: node.lastName, avatarUrl: node.avatarUrl }}
        size="h-10 w-10"
        className="border-2 border-primary/20 shadow"
      />
      <span className="text-sm font-semibold text-center leading-tight truncate max-w-[150px]">{name}</span>
      {node.designation && (
        <span className="text-xs text-muted-foreground text-center leading-tight truncate max-w-[150px]">
          {node.designation}
        </span>
      )}
      {node.departmentName && (
        <span className="text-[10px] text-primary/70 bg-primary/5 rounded px-2 py-0.5 mt-0.5 truncate max-w-[150px]">
          {node.departmentName}
        </span>
      )}
    </div>
  );
}

function renderNodes(nodes: EmpNode[]): React.ReactNode {
  return nodes.map((node) => (
    <TreeNode key={node.employeeId} label={<EmployeeCard node={node} />}>
      {renderNodes(node.children)}
    </TreeNode>
  ));
}

export default function OrgChartPage() {
  // Fetch all employees (large page for full org tree)
  const { data, isLoading, isError, refetch } = useListEmployees({ page: 1, pageSize: 500 });

  const roots = useMemo<EmpNode[]>(() => {
    if (!data?.items) return [];
    const empMap = new Map<string, EmpNode>();
    data.items.forEach((e) => {
      empMap.set(e.employeeId, {
        employeeId:    e.employeeId,
        firstName:     e.firstName,
        lastName:      e.lastName,
        designation:   e.designation,
        departmentName: e.departmentName,
        avatarUrl:     e.avatarUrl,
        managerId:     (e as unknown as Record<string, unknown>).managerId as string | null ?? null,
        children:      [],
      });
    });

    const roots: EmpNode[] = [];
    empMap.forEach((node) => {
      if (node.managerId && empMap.has(node.managerId)) {
        empMap.get(node.managerId)!.children.push(node);
      } else {
        roots.push(node);
      }
    });
    return roots;
  }, [data]);

  return (
    <div className="space-y-6">
      <PageHeader
        title="Org Chart"
        description="Visual hierarchy of your organisation. Scroll and zoom to explore."
      />

      {isLoading ? (
        <div className="space-y-2">
          <Skeleton className="h-16 w-full rounded-xl" />
          <Skeleton className="h-16 w-3/4 mx-auto rounded-xl" />
          <Skeleton className="h-16 w-1/2 mx-auto rounded-xl" />
        </div>
      ) : isError ? (
        <EmptyState title="Failed to load org chart" onRetry={refetch} />
      ) : roots.length === 0 ? (
        <EmptyState title="No employees found" description="Add employees to see the org chart." />
      ) : (
        <div className="overflow-auto border rounded-xl bg-muted/20 p-6">
          <Tree
            lineWidth="2px"
            lineColor="hsl(var(--border))"
            lineBorderRadius="8px"
            label={<span className="text-sm font-bold text-muted-foreground">Organisation</span>}
          >
            {renderNodes(roots)}
          </Tree>
        </div>
      )}
    </div>
  );
}
