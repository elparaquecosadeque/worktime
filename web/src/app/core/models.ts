// Mirrors the API's DTOs (enums travel as strings).

export type Role = 'Worker' | 'Supervisor' | 'Admin';
export type WorkLogStatus = 'Pending' | 'NeedsRevision' | 'Approved' | 'Rejected';
export type WorkLogSource = 'Punch' | 'Manual';
export type Decision = 'Approve' | 'RequestRevision' | 'Reject';
export type PresenceState = 'Offline' | 'Online' | 'Working';

export const Perms = {
  PunchSelf: 'punch:self',
  WorkLogsSubmit: 'worklogs:submit',
  WorkLogsApprove: 'worklogs:approve',
  AssignmentsRequest: 'assignments:request',
  AssignmentsResolve: 'assignments:resolve',
  WorkersManage: 'workers:manage',
  UsersManage: 'users:manage',
  TeamView: 'team:view',
  MonitorAll: 'monitor:all',
  PermissionsManage: 'permissions:manage',
} as const;

export interface UserDto {
  id: string; name: string; email: string; role: Role; isActive: boolean;
  supervisorId: string | null; supervisorName: string | null; timeZoneId: string;
}

export interface PendingRequestDto { id: string; createdAt: string; note: string | null; preferredSupervisorId: string | null; }

export interface MeDto { user: UserDto; permissions: string[]; workingSince: string | null; pendingRequest: PendingRequestDto | null; }

export interface LoginResult { token: string; expiresAt: string; user: UserDto; permissions: string[]; }

export interface DemoAccount { role: Role; name: string; email: string; password: string; hint: string; }

export interface WorkLogEventDto { at: string; actorId: string; actorName: string; from: WorkLogStatus | null; to: WorkLogStatus; reason: string | null; }

export interface WorkLogDto {
  id: string; workerId: string; workerName: string; workerTimeZoneId: string;
  startAt: string; endAt: string; source: WorkLogSource; note: string | null; status: WorkLogStatus;
  history: WorkLogEventDto[];
}

export type BatchOutcome = 'Ok' | 'Conflict' | 'Forbidden' | 'Invalid' | 'NotFound';
export interface BatchItemResult { workLogId: string; outcome: BatchOutcome; code: string | null; }

export interface TeamMemberDto {
  workerId: string; name: string; timeZoneId: string; presence: PresenceState; workingSince: string | null;
  monthHoursApproved: number; monthHoursPending: number; pendingCount: number; needsRevisionCount: number;
}
export interface TeamGroupDto { supervisorId: string | null; supervisorName: string | null; members: TeamMemberDto[]; }
export interface TeamSummaryDto { generatedAt: string; groups: TeamGroupDto[]; }

export interface SupervisorRow { id: string; name: string; }

export interface AssignmentRequestDto {
  id: string; workerId: string; workerName: string; note: string | null;
  preferredSupervisorId: string | null; preferredSupervisorName: string | null; createdAt: string;
}

export interface MatrixCell { role: Role; permission: string; granted: boolean; locked: boolean; }
export interface MatrixDto { permissions: string[]; cells: MatrixCell[]; activeUsersByRole: Partial<Record<Role, number>>; }
export interface SaveMatrixResult { changedRoles: Role[]; usersSignedOut: number; }

export interface Problem { status: number; code: string; codes?: string[]; correlationId?: string; }
