import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { Api } from '../../core/api';
import { Toasts } from '../../core/feedback';
import { MatrixDto, Problem, Role } from '../../core/models';
import { Icon } from '../../ui/icon';

const ROLES: Role[] = ['Worker', 'Supervisor', 'Admin'];

/** What each permission lets a role do, in the product's words. */
function describe(p: string): { name: string; detail: string } {
  switch (p) {
    case 'punch:self': return { name: $localize`:@@perm.punch:Marcar entrada y salida`, detail: $localize`:@@perm.punchD:Usar el reloj de Hoy.` };
    case 'worklogs:submit': return { name: $localize`:@@perm.submit:Registrar y corregir horas propias`, detail: $localize`:@@perm.submitD:Logs manuales y correcciones con razón.` };
    case 'worklogs:approve': return { name: $localize`:@@perm.approve:Decidir sobre registros`, detail: $localize`:@@perm.approveD:Solo de su equipo, salvo que también vea todos los equipos.` };
    case 'assignments:request': return { name: $localize`:@@perm.request:Pedir supervisor`, detail: $localize`:@@perm.requestD:Cuando no tiene uno.` };
    case 'assignments:resolve': return { name: $localize`:@@perm.resolve:Resolver solicitudes`, detail: $localize`:@@perm.resolveD:Asignar o descartar.` };
    case 'workers:manage': return { name: $localize`:@@perm.workers:Gestionar su equipo`, detail: $localize`:@@perm.workersD:Crear, editar, desactivar y desvincular a sus trabajadores.` };
    case 'users:manage': return { name: $localize`:@@perm.users:Gestionar todos los usuarios`, detail: $localize`:@@perm.usersD:Incluye supervisores, asignar y reactivar.` };
    case 'team:view': return { name: $localize`:@@perm.team:Ver equipo en vivo`, detail: $localize`:@@perm.teamD:Presencia y horas del mes.` };
    case 'monitor:all': return { name: $localize`:@@perm.monitor:Ver todos los equipos`, detail: $localize`:@@perm.monitorD:Y los trabajadores sin supervisor.` };
    case 'permissions:manage': return { name: $localize`:@@perm.permissions:Editar esta matriz`, detail: '' };
    default: return { name: p, detail: '' };
  }
}

@Component({
  selector: 'app-matrix',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  template: `
    <h1 class="text-2xl font-extrabold sm:text-3xl" i18n="@@matrix.title">Permisos por rol</h1>
    <p class="mt-1 max-w-prose text-ink-2" i18n="@@matrix.lede">Los permisos viajan dentro de la sesión de cada persona. Al guardar, quien tenga un rol modificado tendrá que volver a entrar.</p>

    @if (data(); as m) {
      <div class="mt-6 overflow-x-auto">
        <table class="w-full min-w-[36rem] border-collapse text-left">
          <thead>
            <tr class="border-b-2 border-ink">
              <th scope="col" class="py-2 pr-4 text-sm font-bold uppercase tracking-[0.06em] text-ink-2" i18n="@@matrix.permission">Permiso</th>
              @for (r of roles; track r) {
                <th scope="col" class="w-28 px-2 py-2 text-center text-sm font-bold uppercase tracking-[0.06em]">
                  {{ roleLabel(r) }}<span class="block text-xs font-semibold normal-case tracking-normal text-ink-3" i18n="@@matrix.people">{{ m.activeUsersByRole[r] ?? 0 }} personas</span>
                </th>
              }
            </tr>
          </thead>
          <tbody>
            @for (p of m.permissions; track p) {
              <tr class="border-b border-rule">
                <th scope="row" class="py-3 pr-4 font-normal">
                  <span class="block font-bold">{{ describe(p).name }}</span>
                  @if (describe(p).detail) { <span class="block text-sm text-ink-2">{{ describe(p).detail }}</span> }
                  <code class="text-xs text-ink-3">{{ p }}</code>
                </th>
                @for (r of roles; track r) {
                  <td class="px-2 py-3 text-center">
                    @if (isLocked(r, p)) {
                      <span class="inline-flex items-center gap-1 text-ink-2" i18n-title="@@matrix.lockedTitle" title="Bloqueado: el admin nunca pierde este permiso">
                        <app-icon name="lock" [size]="16" /><span class="sr-only" i18n="@@matrix.locked">Bloqueado</span>
                      </span>
                    } @else {
                      <input type="checkbox" class="size-5 cursor-pointer" [checked]="isOn(r, p)" (change)="toggle(r, p)"
                        [attr.aria-label]="roleLabel(r) + ': ' + describe(p).name" />
                      @if (changed(r, p)) { <span class="sr-only" i18n="@@matrix.changed">cambiado</span> }
                    }
                  </td>
                }
              </tr>
            }
          </tbody>
        </table>
      </div>

      <div class="sticky bottom-20 mt-6 flex flex-wrap items-center gap-4 rounded-[2px] px-4 py-3 md:bottom-4"
        [class]="dirtyRoles().length ? 'bg-ink text-ground shadow-lift' : 'shadow-[inset_0_0_0_1px_var(--color-rule)]'">
        @if (dirtyRoles().length) {
          <p class="flex-1 text-sm font-semibold" i18n="@@matrix.impact">Guardar cerrará la sesión de {{ impact() }} personas para que entren con sus nuevos permisos.</p>
          <button type="button" class="btn btn-sm !bg-transparent !text-ground underline" (click)="reset()" i18n="@@matrix.discard">Descartar cambios</button>
          <button type="button" class="btn btn-sm !bg-ground !text-ink" (click)="save()" [disabled]="busy()" i18n="@@common.save">Guardar</button>
        } @else {
          <p class="text-sm text-ink-2" i18n="@@matrix.clean">Sin cambios.</p>
        }
      </div>
    } @else {
      <div class="mt-6 skeleton h-96"></div>
    }`,
})
export class MatrixPage {
  private api = inject(Api);
  private toasts = inject(Toasts);

  protected roles = ROLES;
  protected describe = describe;
  protected data = signal<MatrixDto | null>(null);
  protected grants = signal(new Set<string>());
  protected busy = signal(false);

  private original = computed(() => new Set(this.data()?.cells.filter(c => c.granted).map(c => `${c.role}|${c.permission}`) ?? []));

  protected dirtyRoles = computed(() => ROLES.filter(r =>
    (this.data()?.permissions ?? []).some(p => this.original().has(`${r}|${p}`) !== this.grants().has(`${r}|${p}`))));

  protected impact = computed(() => this.dirtyRoles().reduce((n, r) => n + (this.data()?.activeUsersByRole[r] ?? 0), 0));

  constructor() { void this.load(); }

  protected roleLabel(r: Role) {
    return r === 'Worker' ? $localize`:@@role.worker:Trabajador` : r === 'Supervisor' ? $localize`:@@role.supervisor:Supervisor` : $localize`:@@role.admin:Admin`;
  }

  protected isLocked = (r: Role, p: string) => this.data()?.cells.find(c => c.role === r && c.permission === p)?.locked ?? false;
  protected isOn = (r: Role, p: string) => this.grants().has(`${r}|${p}`);
  protected changed = (r: Role, p: string) => this.original().has(`${r}|${p}`) !== this.grants().has(`${r}|${p}`);

  protected toggle(r: Role, p: string) {
    this.grants.update(g => { const n = new Set(g); const k = `${r}|${p}`; n.has(k) ? n.delete(k) : n.add(k); return n; });
  }

  protected reset() { this.grants.set(new Set(this.original())); }

  private async load() {
    try {
      this.data.set(await this.api.matrix());
      this.reset();
    } catch (e) {
      this.toasts.problem(e as Problem);
    }
  }

  protected async save() {
    this.busy.set(true);
    try {
      const grants = [...this.grants()].map(k => { const [role, permission] = k.split('|'); return { role: role as Role, permission }; });
      const r = await this.api.saveMatrix(grants);
      this.toasts.show($localize`:@@matrix.saved:Permisos guardados. ${r.usersSignedOut}:n: personas deben volver a entrar.`, 'ok');
      await this.load(); // if the admin role changed, ForceLogout arrives right after
    } catch (e) {
      this.toasts.problem(e as Problem);
    } finally {
      this.busy.set(false);
    }
  }
}
