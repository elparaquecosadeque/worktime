import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { firstValueFrom, Observable } from 'rxjs';
import {
  AssignmentRequestDto, BatchItemResult, Decision, DemoAccount, LoginResult, MatrixDto, MeDto, Problem, Role,
  SaveMatrixResult, SupervisorRow, TeamSummaryDto, UserDto, WorkLogDto,
} from './models';

/** Thin typed client. Every call resolves or rejects with a {@link Problem} carrying the API's stable code. */
@Injectable({ providedIn: 'root' })
export class Api {
  private http = inject(HttpClient);

  private async call<T>(req: Observable<T>): Promise<T> {
    try {
      return await firstValueFrom(req);
    } catch (e) {
      throw toProblem(e);
    }
  }

  // auth
  login = (email: string, password: string) => this.call(this.http.post<LoginResult>('/api/auth/login', { email, password }));
  demoAccounts = () => this.call(this.http.get<DemoAccount[]>('/api/auth/demo-accounts'));
  me = () => this.call(this.http.get<MeDto>('/api/me'));
  supervisors = () => this.call(this.http.get<SupervisorRow[]>('/api/supervisors'));

  // punch
  punchIn = () => this.call(this.http.post<{ workingSince: string | null }>('/api/punch/in', null));
  punchOut = () => this.call(this.http.post<{ workingSince: null; createdWorkLogIds: string[] }>('/api/punch/out', null));

  // work logs
  myMonth = (year: number, month: number) =>
    this.call(this.http.get<WorkLogDto[]>('/api/worklogs/mine', { params: { year, month } }));
  createLog = (body: { startAt: string; endAt: string; note: string | null }) =>
    this.call(this.http.post<{ id: string }>('/api/worklogs', body));
  editLog = (id: string, body: { startAt: string; endAt: string; note: string | null; reason: string }) =>
    this.call(this.http.put<void>(`/api/worklogs/${id}`, body));
  inbox = (filter: { orphans?: boolean; workerId?: string | null }) => {
    let params = new HttpParams();
    if (filter.orphans) params = params.set('orphans', true);
    if (filter.workerId) params = params.set('workerId', filter.workerId);
    return this.call(this.http.get<WorkLogDto[]>('/api/worklogs/inbox', { params }));
  };
  decide = (id: string, decision: Decision, reason: string | null) =>
    this.call(this.http.post<void>(`/api/worklogs/${id}/decision`, { decision, reason }));
  decideBatch = (workLogIds: string[], decision: Decision, reason: string | null) =>
    this.call(this.http.post<BatchItemResult[]>('/api/worklogs/decisions', { workLogIds, decision, reason }));

  // team
  teamSummary = () => this.call(this.http.get<TeamSummaryDto>('/api/team/summary'));

  // users
  users = () => this.call(this.http.get<UserDto[]>('/api/users'));
  createUser = (body: { name: string; email: string; password: string; role: Role; timeZoneId: string | null }) =>
    this.call(this.http.post<UserDto>('/api/users', body));
  updateUser = (id: string, body: { name: string; email: string; timeZoneId: string }) =>
    this.call(this.http.put<UserDto>(`/api/users/${id}`, body));
  setActive = (id: string, active: boolean) =>
    this.call(this.http.post<UserDto>(`/api/users/${id}/${active ? 'activate' : 'deactivate'}`, null));
  unassign = (id: string) => this.call(this.http.post<UserDto>(`/api/users/${id}/unassign`, null));
  assign = (id: string, supervisorId: string) => this.call(this.http.post<UserDto>(`/api/users/${id}/assign`, { supervisorId }));

  // assignments
  pendingRequests = () => this.call(this.http.get<AssignmentRequestDto[]>('/api/assignments'));
  requestAssignment = (note: string | null, preferredSupervisorId: string | null) =>
    this.call(this.http.post<{ id: string }>('/api/assignments', { note, preferredSupervisorId }));
  fulfill = (id: string, supervisorId: string) => this.call(this.http.post<void>(`/api/assignments/${id}/fulfill`, { supervisorId }));
  dismiss = (id: string, reason: string) => this.call(this.http.post<void>(`/api/assignments/${id}/dismiss`, { reason }));

  // permissions
  matrix = () => this.call(this.http.get<MatrixDto>('/api/permissions'));
  saveMatrix = (grants: { role: Role; permission: string }[]) =>
    this.call(this.http.put<SaveMatrixResult>('/api/permissions', { grants }));
}

export function toProblem(e: unknown): Problem {
  if (e instanceof HttpErrorResponse) {
    const body = e.error as Partial<Problem> | null;
    const code = body?.code ?? (e.status === 0 ? 'network.offline' : e.status === 429 ? 'rate.limited' : e.status === 401 ? 'auth.required' : e.status === 403 ? 'auth.forbidden' : 'server.error');
    return { status: e.status, code, codes: body?.codes, correlationId: body?.correlationId };
  }
  return { status: 0, code: 'client.error' };
}
