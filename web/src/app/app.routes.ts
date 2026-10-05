import { Routes } from '@angular/router';
import { requireLogin, requirePerm } from './core/auth';
import { Perms } from './core/models';
import { Shell } from './shell/shell';

// One route per screen, gated by permission claims (not roles): the admin's matrix decides who sees what.
export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login').then(m => m.LoginPage) },
  {
    path: '',
    component: Shell,
    canActivate: [requireLogin],
    children: [
      { path: 'hoy', canActivate: [requirePerm(Perms.PunchSelf)], loadComponent: () => import('./pages/worker/today').then(m => m.TodayPage) },
      { path: 'mes', canActivate: [requirePerm(Perms.WorkLogsSubmit)], loadComponent: () => import('./pages/worker/month').then(m => m.MonthPage) },
      { path: 'equipo', canActivate: [requirePerm(Perms.TeamView)], loadComponent: () => import('./pages/team/team').then(m => m.TeamPage) },
      { path: 'bandeja', canActivate: [requirePerm(Perms.WorkLogsApprove)], loadComponent: () => import('./pages/inbox/inbox').then(m => m.InboxPage) },
      { path: 'personas', canActivate: [requirePerm(Perms.WorkersManage, Perms.UsersManage)], loadComponent: () => import('./pages/people/people').then(m => m.PeoplePage) },
      { path: 'solicitudes', canActivate: [requirePerm(Perms.AssignmentsResolve)], loadComponent: () => import('./pages/admin/requests').then(m => m.RequestsPage) },
      { path: 'permisos', canActivate: [requirePerm(Perms.PermissionsManage)], loadComponent: () => import('./pages/admin/matrix').then(m => m.MatrixPage) },
      { path: '', pathMatch: 'full', loadComponent: () => import('./pages/landing').then(m => m.Landing) },
    ],
  },
  { path: '**', redirectTo: '' },
];
