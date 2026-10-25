import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../core/api';
import { messageFor } from '../core/feedback';
import { Problem } from '../core/models';
import { urlOnHost } from '../core/tenancy';
import { browserZone } from '../core/time';
import { Icon } from '../ui/icon';
import { StationClock } from '../ui/station-clock';

/** Root host: each organization lives on its own subdomain, so the first step is saying which one. */
@Component({
  selector: 'app-tenant-picker',
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
            <p class="mt-3 text-base leading-relaxed text-ground/75" i18n="@@picker.lede">Cada organización tiene su propio espacio, con sus personas, sus horas y sus permisos.</p>
          </div>
        </div>
        <p class="text-sm text-ground/60" i18n="@@login.demoNote">Demo pública: los datos se reinician cada 24 horas.</p>
      </section>

      <section class="flex items-center justify-center px-6 py-10 sm:px-10">
        <div class="w-full max-w-sm space-y-8">
          <form class="space-y-5" (ngSubmit)="go(slug)" novalidate>
            <h2 class="text-2xl font-extrabold" i18n="@@picker.heading">¿A qué espacio entras?</h2>
            <label class="field">
              <span i18n="@@picker.slug">Nombre del espacio</span>
              <input class="input" name="slug" autocomplete="organization" autocapitalize="none" spellcheck="false" required
                i18n-placeholder="@@picker.slugPlaceholder" placeholder="tu-empresa"
                [(ngModel)]="slug" [attr.aria-invalid]="error() ? 'true' : null" />
            </label>
            @if (error(); as message) {
              <p class="text-sm font-semibold text-stop" role="alert">{{ message }}</p>
            }
            <button type="submit" class="btn w-full" [disabled]="busy() || !slug.trim()">
              <span i18n="@@picker.submit">Ir a mi espacio</span><app-icon name="arrow" [size]="18" />
            </button>
          </form>

          <div class="space-y-3 border-t border-rule pt-6">
            <p class="text-sm text-ink-2" i18n="@@picker.demoLede">¿Solo quieres verlo? Entra al espacio de demo con cuentas listas para cada rol.</p>
            <button type="button" class="btn btn-quiet w-full" [disabled]="busy()" (click)="go('demo')" i18n="@@picker.demo">Probar la demo</button>
          </div>
        </div>
      </section>
    </div>`,
})
export class TenantPickerPage {
  private api = inject(Api);

  protected zone = browserZone();
  protected clockLabel = $localize`:@@login.clockLabel:Reloj con la hora actual`;
  protected slug = '';
  protected busy = signal(false);
  protected error = signal<string | null>(null);

  protected async go(slug: string) {
    this.busy.set(true);
    this.error.set(null);
    try {
      const tenant = await this.api.findTenant(slug.trim().toLowerCase());
      location.assign(urlOnHost(tenant.host, 'login'));
    } catch (e) {
      this.error.set(messageFor((e as Problem).code));
      this.busy.set(false);
    }
  }
}
