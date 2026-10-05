import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Api } from '../core/api';
import { Auth } from '../core/auth';
import { messageFor } from '../core/feedback';
import { DemoAccount, Problem, Role } from '../core/models';
import { browserZone } from '../core/time';
import { Icon } from '../ui/icon';
import { StationClock } from '../ui/station-clock';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, Icon, StationClock],
  template: `
    <div class="grid min-h-dvh lg:grid-cols-[1.05fr_1fr]">
      <section class="relative flex flex-col justify-between gap-10 bg-ink px-6 py-8 text-ground sm:px-10 lg:py-12">
        <div class="flex items-center gap-2.5">
          <span class="text-xl font-extrabold tracking-tight">Worktime</span>
        </div>
        <div class="flex flex-col items-start gap-8 sm:flex-row sm:items-center lg:flex-col lg:items-start">
          <app-station-clock [zone]="zone" class="size-40 shrink-0 sm:size-48 lg:size-64" [label]="clockLabel" />
          <div class="max-w-md">
            <h1 class="text-3xl font-extrabold leading-tight sm:text-4xl" i18n="@@login.title">Las horas de tu equipo, en vivo y sin aprobaciones dobles.</h1>
            <p class="mt-3 text-base leading-relaxed text-ground/75" i18n="@@login.lede">Marca entrada y salida, revisa el estado de cada registro y decide sobre las horas de tu equipo mientras pasan.</p>
          </div>
        </div>
        <p class="text-sm text-ground/60" i18n="@@login.demoNote">Demo pública: los datos se reinician cada 24 horas.</p>
      </section>

      <section class="flex items-center justify-center px-6 py-10 sm:px-10">
        <form class="w-full max-w-sm space-y-5" (ngSubmit)="submit()" novalidate>
          <h2 class="text-2xl font-extrabold" i18n="@@login.heading">Entrar</h2>

          @if (ended(); as reason) {
            <p class="flex items-start gap-2 rounded-[2px] bg-wait px-3 pb-2 pt-2.5 text-sm font-semibold text-ink" role="alert">
              <app-icon name="signal" [size]="16" class="mt-px" />{{ reason }}
            </p>
          }

          @if (accounts().length) {
            <label class="field">
              <span i18n="@@login.demoAccount">Cuenta de demo</span>
              <select class="input" [ngModel]="picked()" (ngModelChange)="pick($event)" name="demo">
                <option [ngValue]="null" i18n="@@login.pickRole">Elige un rol para llenar los datos…</option>
                @for (group of grouped(); track group.role) {
                  <optgroup [label]="group.label">
                    @for (a of group.accounts; track a.email) {
                      <option [ngValue]="a">{{ a.name }} · {{ a.hint }}</option>
                    }
                  </optgroup>
                }
              </select>
            </label>
          }

          <label class="field">
            <span i18n="@@login.email">Email</span>
            <input class="input" type="email" name="email" autocomplete="username" required [(ngModel)]="email" [attr.aria-invalid]="error() ? 'true' : null" />
          </label>
          <label class="field">
            <span i18n="@@login.password">Contraseña</span>
            <input class="input" type="password" name="password" autocomplete="current-password" required [(ngModel)]="password" [attr.aria-invalid]="error() ? 'true' : null" />
          </label>

          @if (error(); as message) {
            <p class="text-sm font-semibold text-stop" role="alert">{{ message }}</p>
          }

          <button type="submit" class="btn w-full" [disabled]="busy() || !email || !password">
            @if (busy()) { <span i18n="@@login.signingIn">Entrando…</span> } @else { <span i18n="@@login.submit">Entrar</span><app-icon name="arrow" [size]="18" /> }
          </button>
        </form>
      </section>
    </div>`,
})
export class LoginPage {
  private api = inject(Api);
  private auth = inject(Auth);
  private router = inject(Router);

  protected zone = browserZone();
  protected clockLabel = $localize`:@@login.clockLabel:Reloj con la hora actual`;
  protected email = '';
  protected password = '';
  protected busy = signal(false);
  protected error = signal<string | null>(null);
  protected accounts = signal<DemoAccount[]>([]);
  protected picked = signal<DemoAccount | null>(null);
  protected ended = computed(() => { const r = this.auth.endedReason(); return r ? messageFor(r) : null; });

  protected grouped = computed(() => {
    const labels: Record<Role, string> = {
      Worker: $localize`:@@role.workers:Trabajadores`,
      Supervisor: $localize`:@@role.supervisors:Supervisores`,
      Admin: $localize`:@@role.admin:Admin`,
    };
    return (['Worker', 'Supervisor', 'Admin'] as Role[])
      .map(role => ({ role, label: labels[role], accounts: this.accounts().filter(a => a.role === role) }))
      .filter(g => g.accounts.length);
  });

  constructor() {
    if (this.auth.isLoggedIn()) void this.router.navigateByUrl(this.auth.home());
    this.api.demoAccounts().then(a => this.accounts.set(a), () => undefined);
  }

  protected pick(a: DemoAccount | null) {
    this.picked.set(a);
    if (!a) return;
    this.email = a.email;
    this.password = a.password;
    this.error.set(null);
  }

  protected async submit() {
    this.busy.set(true);
    this.error.set(null);
    try {
      const r = await this.api.login(this.email, this.password);
      this.auth.signIn(r.token, r.expiresAt, r.user, r.permissions);
      await this.router.navigateByUrl(this.auth.home());
    } catch (e) {
      this.error.set(messageFor((e as Problem).code));
    } finally {
      this.busy.set(false);
    }
  }
}
