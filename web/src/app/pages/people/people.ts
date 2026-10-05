import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Dialog } from '@angular/cdk/dialog';
import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { Toasts } from '../../core/feedback';
import { Perms, Problem, Role, SupervisorRow, UserDto } from '../../core/models';
import { Realtime } from '../../core/realtime';
import { Icon } from '../../ui/icon';
import { UserForm, UserFormData } from './user-form';

type Pending = { id: string; kind: 'deactivate' | 'unassign' | 'assign'; supervisorId?: string | null };

/**
 * Supervisors (workers:manage) see their own workers; admins (users:manage) see everyone.
 * Nobody is deleted: deactivation keeps history and ends their sessions at once.
 */
@Component({
  selector: 'app-people',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, Icon],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="text-2xl font-extrabold sm:text-3xl">
          @if (admin()) { <span i18n="@@people.titleAdmin">Usuarios</span> } @else { <span i18n="@@people.title">Tus trabajadores</span> }
        </h1>
        <p class="mt-1 text-sm font-semibold text-ink-2" i18n="@@people.counts">{{ activeCount() }} activos · {{ users().length - activeCount() }} desactivados</p>
      </div>
      <button type="button" class="btn" (click)="openForm()"><app-icon name="plus" [size]="18" />
        @if (admin()) { <ng-container i18n="@@people.new">Nueva persona</ng-container> } @else { <ng-container i18n="@@people.newWorker">Nuevo trabajador</ng-container> }
      </button>
    </div>

    @if (admin()) {
      <div class="mt-5 flex flex-wrap gap-2" role="group" i18n-aria-label="@@people.filter" aria-label="Filtrar por rol">
        @for (f of filters; track f.value) {
          <button type="button" class="seg" [class.is-on]="roleFilter() === f.value" (click)="roleFilter.set(f.value)">{{ f.label }}</button>
        }
      </div>
    }

    <ul class="mt-5 divide-y divide-rule border-y border-rule">
      @for (u of visible(); track u.id) {
        @let p = pending()?.id === u.id ? pending() : null;
        <li class="grid gap-x-4 gap-y-2 py-4 md:grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)_auto] md:items-center" [class.opacity-60]="!u.isActive">
          <div class="min-w-0">
            <div class="flex flex-wrap items-center gap-2">
              <span class="font-bold">{{ u.name }}</span>
              @if (admin()) { <span class="plate text-ink shadow-[inset_0_0_0_1px_var(--color-ink)]">{{ roleLabel(u.role) }}</span> }
              @if (!u.isActive) { <span class="plate bg-idle text-white" i18n="@@people.inactive">Desactivado</span> }
            </div>
            <div class="truncate text-sm text-ink-2">{{ u.email }} · {{ u.timeZoneId }}</div>
          </div>
          <div class="text-sm">
            @if (u.role === 'Worker' && admin()) {
              @if (u.supervisorName) { <span class="inline-flex items-center gap-1.5"><app-icon name="link" [size]="15" />{{ u.supervisorName }}</span> }
              @else { <span class="plate bg-wait text-ink" i18n="@@team.orphans">Sin supervisor</span> }
            }
          </div>
          <div class="flex flex-wrap justify-start gap-2 md:justify-end">
            @if (p) {
              @switch (p.kind) {
                @case ('deactivate') {
                  <span class="self-center text-sm font-semibold" i18n="@@people.confirmDeactivate">Su sesión se cerrará al instante.</span>
                  <button type="button" class="btn btn-stop btn-sm" (click)="run(u, 'deactivate')" i18n="@@people.deactivate">Desactivar</button>
                }
                @case ('unassign') {
                  <span class="self-center text-sm font-semibold" i18n="@@people.confirmUnassign">Quedará sin supervisor; sus pendientes pasan al admin.</span>
                  <button type="button" class="btn btn-sm" (click)="run(u, 'unassign')" i18n="@@people.unassign">Desvincular</button>
                }
                @case ('assign') {
                  <select class="input !min-h-8 !w-auto !py-1 text-sm" [ngModel]="p.supervisorId" (ngModelChange)="pending.set({ id: u.id, kind: 'assign', supervisorId: $event })"
                    i18n-aria-label="@@people.supervisorPick" aria-label="Supervisor">
                    <option [ngValue]="null" i18n="@@people.pickSupervisor">Elige supervisor…</option>
                    @for (s of supervisors(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
                  </select>
                  <button type="button" class="btn btn-sm" (click)="run(u, 'assign')" [disabled]="!p.supervisorId" i18n="@@people.assign">Asignar</button>
                }
              }
              <button type="button" class="btn btn-quiet btn-sm" (click)="pending.set(null)" i18n="@@common.cancel">Cancelar</button>
            } @else {
              @if (u.isActive) {
                <button type="button" class="btn btn-quiet btn-sm" (click)="openForm(u)"><app-icon name="pencil" [size]="15" /><ng-container i18n="@@common.edit">Editar</ng-container></button>
                @if (u.role === 'Worker' && admin()) {
                  <button type="button" class="btn btn-quiet btn-sm" (click)="pending.set({ id: u.id, kind: 'assign', supervisorId: u.supervisorId })">
                    <app-icon name="link" [size]="15" />@if (u.supervisorId) { <ng-container i18n="@@people.reassign">Reasignar</ng-container> } @else { <ng-container i18n="@@people.assign">Asignar</ng-container> }
                  </button>
                }
                @if (u.role === 'Worker' && u.supervisorId) {
                  <button type="button" class="btn btn-quiet btn-sm" (click)="pending.set({ id: u.id, kind: 'unassign' })"><app-icon name="unlink" [size]="15" /><ng-container i18n="@@people.unassign">Desvincular</ng-container></button>
                }
                @if (u.id !== me()) {
                  <button type="button" class="btn btn-quiet btn-sm !text-stop" (click)="pending.set({ id: u.id, kind: 'deactivate' })"><app-icon name="power" [size]="15" /><ng-container i18n="@@people.deactivate">Desactivar</ng-container></button>
                }
              } @else if (admin()) {
                <button type="button" class="btn btn-quiet btn-sm" (click)="run(u, 'activate')"><app-icon name="power" [size]="15" /><ng-container i18n="@@people.reactivate">Reactivar</ng-container></button>
              }
            }
          </div>
        </li>
      } @empty {
        <li class="py-10">
          <p class="text-lg font-bold" i18n="@@people.emptyTitle">Aún no hay nadie aquí.</p>
          <p class="mt-1 text-ink-2" i18n="@@people.emptyBody">Crea a la primera persona: recibirá una contraseña inicial para entrar.</p>
        </li>
      }
    </ul>`,
  styles: `
    .seg { padding: .375rem .75rem .25rem; font-size: .875rem; font-weight: 700; border-radius: 2px; color: var(--color-ink-2); box-shadow: inset 0 0 0 1px var(--color-rule); }
    .seg.is-on { background: var(--color-ink); color: var(--color-ground); box-shadow: none; }
  `,
})
export class PeoplePage {
  private api = inject(Api);
  private auth = inject(Auth);
  private toasts = inject(Toasts);
  private dialog = inject(Dialog);

  protected users = signal<UserDto[]>([]);
  protected supervisors = signal<SupervisorRow[]>([]);
  protected pending = signal<Pending | null>(null);
  protected roleFilter = signal<Role | 'all'>('all');
  protected admin = computed(() => this.auth.has(Perms.UsersManage));
  protected me = computed(() => this.auth.user()?.userId);
  protected activeCount = computed(() => this.users().filter(u => u.isActive).length);
  protected visible = computed(() => this.roleFilter() === 'all' ? this.users() : this.users().filter(u => u.role === this.roleFilter()));

  protected filters: { value: Role | 'all'; label: string }[] = [
    { value: 'all', label: $localize`:@@people.all:Todos` },
    { value: 'Worker', label: $localize`:@@role.workers:Trabajadores` },
    { value: 'Supervisor', label: $localize`:@@role.supervisors:Supervisores` },
    { value: 'Admin', label: $localize`:@@role.admin:Admin` },
  ];

  constructor() {
    const realtime = inject(Realtime);
    realtime.on(['AssignmentChanged'], () => void this.load());
    realtime.onResync(() => void this.load());
    void this.load();
    this.api.supervisors().then(s => this.supervisors.set(s), () => undefined);
  }

  protected roleLabel(r: Role) {
    return r === 'Worker' ? $localize`:@@role.worker:Trabajador` : r === 'Supervisor' ? $localize`:@@role.supervisor:Supervisor` : $localize`:@@role.admin:Admin`;
  }

  private async load() {
    try {
      this.users.set(await this.api.users());
    } catch (e) {
      this.toasts.problem(e as Problem);
    }
  }

  protected openForm(user?: UserDto) {
    const data: UserFormData = { user, canChooseRole: this.admin() };
    this.dialog.open<boolean>(UserForm, { data, backdropClass: 'cdk-overlay-dark-backdrop' }).closed.subscribe(saved => {
      if (saved) { this.toasts.show($localize`:@@people.saved:Cambios guardados.`, 'ok'); void this.load(); }
    });
  }

  protected async run(u: UserDto, kind: 'deactivate' | 'activate' | 'unassign' | 'assign') {
    try {
      if (kind === 'deactivate') await this.api.setActive(u.id, false);
      if (kind === 'activate') await this.api.setActive(u.id, true);
      if (kind === 'unassign') await this.api.unassign(u.id);
      if (kind === 'assign') await this.api.assign(u.id, this.pending()!.supervisorId!);
      this.pending.set(null);
      await this.load();
    } catch (e) {
      this.toasts.problem(e as Problem);
    }
  }
}
