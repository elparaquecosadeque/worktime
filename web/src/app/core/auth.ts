import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { Perms, Role } from './models';

interface Session { token: string; expiresAt: string; userId: string; name: string; role: Role; permissions: string[]; }

const KEY = 'worktime.session';

/**
 * Holds the JWT (localStorage, see README for the XSS trade-off) and the claims the UI needs.
 * Claims only drive the UX — the server re-checks every permission and ownership.
 */
@Injectable({ providedIn: 'root' })
export class Auth {
  private router = inject(Router);
  private session = signal<Session | null>(read());

  /** Why the last session ended (shown on the login screen). */
  readonly endedReason = signal<string | null>(null);

  readonly token = computed(() => this.session()?.token ?? null);
  readonly user = computed(() => this.session());
  readonly isLoggedIn = computed(() => this.session() !== null);
  private readonly perms = computed(() => new Set(this.session()?.permissions ?? []));

  has = (...permissions: string[]) => permissions.some(p => this.perms().has(p));

  /** Where each person lands: the first screen their permissions open. */
  readonly home = computed(() => {
    if (this.has(Perms.PunchSelf)) return '/hoy';
    if (this.has(Perms.TeamView)) return '/equipo';
    if (this.has(Perms.WorkLogsApprove)) return '/bandeja';
    if (this.has(Perms.UsersManage, Perms.WorkersManage)) return '/personas';
    return '/login';
  });

  signIn(token: string, expiresAt: string, user: { id: string; name: string; role: Role }, permissions: string[]) {
    const s: Session = { token, expiresAt, userId: user.id, name: user.name, role: user.role, permissions };
    try { localStorage.setItem(KEY, JSON.stringify(s)); } catch { /* private mode: session lives in memory */ }
    this.endedReason.set(null);
    this.session.set(s);
  }

  signOut(reason: string | null = null) {
    try { localStorage.removeItem(KEY); } catch { /* ignore */ }
    if (this.session() === null && reason === null) return;
    this.session.set(null);
    this.endedReason.set(reason);
    void this.router.navigateByUrl('/login');
  }
}

function read(): Session | null {
  try {
    const s = JSON.parse(localStorage.getItem(KEY) ?? 'null') as Session | null;
    return s && new Date(s.expiresAt) > new Date() ? s : null;
  } catch {
    return null;
  }
}

/** Adds the bearer token; a 401 on an authenticated call means the session is gone (reset, revoked, expired). */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(Auth);
  const token = auth.token();
  const authed = token && req.url.startsWith('/api/') && !req.url.startsWith('/api/auth/');
  return next(authed ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req).pipe(
    catchError((e: unknown) => {
      if (authed && e instanceof HttpErrorResponse && e.status === 401) auth.signOut('auth.stale_session');
      return throwError(() => e);
    }),
  );
};

export const requireLogin: CanActivateFn = () => {
  const auth = inject(Auth);
  return auth.isLoggedIn() || inject(Router).parseUrl('/login');
};

/** Route guard by permission claim (any of). UX only: the API enforces the same rule. */
export const requirePerm = (...permissions: string[]): CanActivateFn => () => {
  const auth = inject(Auth);
  if (!auth.isLoggedIn()) return inject(Router).parseUrl('/login');
  return auth.has(...permissions) || inject(Router).parseUrl(auth.home());
};
