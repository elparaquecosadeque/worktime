import { ChangeDetectionStrategy, Component, LOCALE_ID, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { Auth } from '../core/auth';
import { Perms } from '../core/models';
import { Realtime } from '../core/realtime';
import { Icon } from '../ui/icon';

interface NavItem { path: string; label: string; icon: string; }

@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Icon],
  template: `
    <a href="#main" class="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 btn" i18n="@@shell.skip">Saltar al contenido</a>

    <header class="sticky top-0 z-30 bg-ink text-ground">
      <div class="mx-auto flex h-14 max-w-6xl items-center gap-4 px-4 sm:px-6">
        <a routerLink="/" class="flex items-center gap-2.5 text-ground no-underline" aria-label="Worktime">
          <svg viewBox="0 0 32 32" class="size-7" aria-hidden="true"><circle cx="16" cy="16" r="14" fill="var(--color-card)"/><rect x="14.8" y="5" width="2.4" height="12" fill="var(--color-ink)"/><rect x="15" y="14.8" width="9" height="2.4" fill="var(--color-ink)"/><rect x="15.4" y="4" width="1.2" height="14" fill="var(--color-stop)" transform="rotate(130 16 16)"/></svg>
          <span class="text-lg font-extrabold tracking-tight">Worktime</span>
        </a>

        <nav class="hidden flex-1 items-stretch self-stretch md:flex" i18n-aria-label="@@shell.nav" aria-label="Secciones">
          @for (item of nav(); track item.path) {
            <a [routerLink]="item.path" routerLinkActive="is-active" class="nav-tab">
              <app-icon [name]="item.icon" [size]="18" />{{ item.label }}
            </a>
          }
        </nav>
        <span class="flex-1 md:hidden"></span>

        <span class="plate" [class]="connection().cls" role="status" [attr.title]="connection().label">
          <span class="size-2 rounded-full" [class]="connection().dot"></span>
          <span class="sr-only sm:not-sr-only">{{ connection().label }}</span>
        </span>

        <a [href]="otherLocale().href" (click)="switchLocale($event)" class="flex items-center gap-1 text-sm font-bold text-ground/80 no-underline hover:text-ground" [attr.lang]="otherLocale().code"
          [attr.aria-label]="otherLocale().aria">
          <app-icon name="globe" [size]="16" />{{ otherLocale().code.toUpperCase() }}
        </a>

        <div class="hidden items-center gap-3 border-l border-ground/20 pl-4 lg:flex">
          <span class="text-sm leading-tight">
            <span class="block font-bold">{{ auth.user()?.name }}</span>
            <span class="block text-ground/70">{{ roleLabel() }}</span>
          </span>
        </div>
        <button type="button" class="flex items-center gap-1.5 text-sm font-bold text-ground/80 hover:text-ground" (click)="auth.signOut()">
          <app-icon name="exit" [size]="18" /><span class="hidden sm:inline" i18n="@@shell.signOut">Salir</span>
        </button>
      </div>
    </header>

    <main id="main" class="mx-auto max-w-6xl px-4 pb-28 pt-6 sm:px-6 md:pb-12 md:pt-8">
      <router-outlet />
    </main>

    <nav class="fixed inset-x-0 bottom-0 z-30 flex border-t border-rule bg-card pb-[env(safe-area-inset-bottom)] md:hidden" i18n-aria-label="@@shell.nav" aria-label="Secciones">
      @for (item of nav(); track item.path) {
        <a [routerLink]="item.path" routerLinkActive="is-active" class="bottom-tab">
          <app-icon [name]="item.icon" [size]="22" /><span>{{ item.label }}</span>
        </a>
      }
    </nav>`,
  styles: `
    .nav-tab { display: flex; align-items: center; gap: .5rem; padding: 0 .875rem; font-weight: 700; font-size: .9375rem;
      color: rgb(242 243 241 / .72); text-decoration: none; box-shadow: inset 0 -3px 0 transparent;
      transition: color 150ms var(--ease-out-expo), box-shadow 150ms var(--ease-out-expo); }
    .nav-tab:hover { color: var(--color-ground); }
    .nav-tab.is-active { color: var(--color-ground); box-shadow: inset 0 -3px 0 var(--color-ground); }
    .bottom-tab { flex: 1; display: flex; flex-direction: column; align-items: center; gap: .125rem; padding: .5rem 0 .375rem;
      font-size: .6875rem; font-weight: 700; color: var(--color-ink-3); text-decoration: none; }
    .bottom-tab.is-active { color: var(--color-ink); box-shadow: inset 0 3px 0 var(--color-ink); }
  `,
})
export class Shell {
  protected auth = inject(Auth);
  private realtime = inject(Realtime);
  private locale = inject(LOCALE_ID);
  private router = inject(Router);

  protected nav = computed<NavItem[]>(() => {
    const a = this.auth;
    const items: NavItem[] = [];
    if (a.has(Perms.PunchSelf)) items.push({ path: '/hoy', label: $localize`:@@nav.today:Hoy`, icon: 'clock' });
    if (a.has(Perms.WorkLogsSubmit)) items.push({ path: '/mes', label: $localize`:@@nav.month:Mes`, icon: 'calendar' });
    if (a.has(Perms.TeamView)) items.push({ path: '/equipo', label: a.has(Perms.MonitorAll) ? $localize`:@@nav.monitor:Equipos` : $localize`:@@nav.team:Equipo`, icon: 'people' });
    if (a.has(Perms.WorkLogsApprove)) items.push({ path: '/bandeja', label: $localize`:@@nav.inbox:Bandeja`, icon: 'inbox' });
    if (a.has(Perms.WorkersManage, Perms.UsersManage)) items.push({ path: '/personas', label: a.has(Perms.UsersManage) ? $localize`:@@nav.users:Usuarios` : $localize`:@@nav.workers:Trabajadores`, icon: 'person' });
    if (a.has(Perms.AssignmentsResolve)) items.push({ path: '/solicitudes', label: $localize`:@@nav.requests:Solicitudes`, icon: 'hand' });
    if (a.has(Perms.PermissionsManage)) items.push({ path: '/permisos', label: $localize`:@@nav.permissions:Permisos`, icon: 'key' });
    return items;
  });

  protected roleLabel = computed(() => {
    switch (this.auth.user()?.role) {
      case 'Worker': return $localize`:@@role.worker:Trabajador`;
      case 'Supervisor': return $localize`:@@role.supervisor:Supervisor`;
      case 'Admin': return $localize`:@@role.admin:Admin`;
      default: return '';
    }
  });

  protected connection = computed(() => {
    switch (this.realtime.state()) {
      case 'live': return { label: $localize`:@@conn.live:En vivo`, cls: 'bg-go text-white', dot: 'bg-white' };
      case 'connecting':
      case 'reconnecting': return { label: $localize`:@@conn.reconnecting:Reconectando`, cls: 'bg-wait text-ink', dot: 'bg-ink motion-safe:animate-pulse' };
      default: return { label: $localize`:@@conn.offline:Sin conexión`, cls: 'bg-stop text-white', dot: 'border border-white' };
    }
  });

  protected otherLocale = computed(() => {
    const isEn = this.locale.startsWith('en');
    const code = isEn ? 'es' : 'en';
    return { code, href: `/${code}/`, aria: isEn ? 'Ver en espa�ol' : 'View in English' };
  });

  /** Locales are separate builds (/es/, /en/): switching reloads the same screen in the other build. */
  protected switchLocale(e: Event) {
    e.preventDefault();
    location.assign(`/${this.otherLocale().code}${this.router.url}`);
  }
}
