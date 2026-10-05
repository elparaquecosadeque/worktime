import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { A11yModule } from '@angular/cdk/a11y';
import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Api } from '../../core/api';
import { messageFor } from '../../core/feedback';
import { Problem, Role, UserDto } from '../../core/models';
import { Icon } from '../../ui/icon';

export interface UserFormData { user?: UserDto; canChooseRole: boolean; }

const ZONES = ['America/Lima', 'America/Bogota', 'America/Mexico_City', 'America/Santiago', 'America/Argentina/Buenos_Aires', 'America/New_York', 'Europe/Madrid', 'UTC'];

@Component({
  selector: 'app-user-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, A11yModule, Icon],
  template: `
    <form class="w-[min(100vw-2rem,30rem)] rounded-[4px] bg-card p-6 shadow-lift" (ngSubmit)="save()" cdkTrapFocus [cdkTrapFocusAutoCapture]="true">
      <div class="flex items-start justify-between gap-4">
        <h2 class="text-xl font-extrabold">
          @if (data.user) { <span i18n="@@userForm.editTitle">Editar persona</span> } @else { <span i18n="@@userForm.createTitle">Nueva persona</span> }
        </h2>
        <button type="button" class="-mr-2 -mt-1 p-2 text-ink-2 hover:text-ink" (click)="ref.close()" i18n-aria-label="@@common.close" aria-label="Cerrar"><app-icon name="cross" /></button>
      </div>
      @if (!data.user && !data.canChooseRole) {
        <p class="mt-1 text-sm text-ink-2" i18n="@@userForm.workerHint">Se crea como trabajador de tu equipo.</p>
      }
      <div class="mt-5 grid gap-4">
        <label class="field"><span i18n="@@userForm.name">Nombre</span><input class="input" name="name" required [(ngModel)]="name" autocomplete="off" /></label>
        <label class="field"><span i18n="@@userForm.email">Email</span><input class="input" type="email" name="email" required [(ngModel)]="email" autocomplete="off" /></label>
        @if (!data.user) {
          <label class="field">
            <span i18n="@@userForm.password">Contraseña inicial</span>
            <input class="input" type="text" name="password" required minlength="8" [(ngModel)]="password" autocomplete="new-password" />
          </label>
          @if (data.canChooseRole) {
            <fieldset class="field">
              <legend class="mb-1.5 text-[0.8125rem] font-semibold text-ink-2" i18n="@@userForm.role">Rol</legend>
              <div class="flex gap-2">
                @for (r of roles; track r.value) {
                  <label class="flex flex-1 cursor-pointer items-center gap-2 rounded-[2px] px-3 py-2 shadow-[inset_0_0_0_1px_var(--color-rule)] has-[:checked]:bg-ink has-[:checked]:text-ground">
                    <input type="radio" name="role" class="sr-only" [value]="r.value" [(ngModel)]="role" />{{ r.label }}
                  </label>
                }
              </div>
            </fieldset>
          }
        }
        <label class="field">
          <span i18n="@@userForm.zone">Zona horaria</span>
          <select class="input" name="zone" [(ngModel)]="zone">@for (z of zones; track z) { <option [value]="z">{{ z }}</option> }</select>
        </label>
      </div>
      @if (error(); as m) { <p class="mt-4 text-sm font-semibold text-stop" role="alert">{{ m }}</p> }
      <div class="mt-6 flex justify-end gap-3">
        <button type="button" class="btn btn-quiet" (click)="ref.close()" i18n="@@common.cancel">Cancelar</button>
        <button type="submit" class="btn" [disabled]="busy()" i18n="@@common.save">Guardar</button>
      </div>
    </form>`,
})
export class UserForm {
  protected data = inject<UserFormData>(DIALOG_DATA);
  protected ref = inject<DialogRef<boolean>>(DialogRef);
  private api = inject(Api);

  protected zones = ZONES;
  protected roles: { value: Role; label: string }[] = [
    { value: 'Worker', label: $localize`:@@role.worker:Trabajador` },
    { value: 'Supervisor', label: $localize`:@@role.supervisor:Supervisor` },
  ];
  protected name = this.data.user?.name ?? '';
  protected email = this.data.user?.email ?? '';
  protected password = '';
  protected role: Role = 'Worker';
  protected zone = this.data.user?.timeZoneId ?? 'America/Lima';
  protected busy = signal(false);
  protected error = signal<string | null>(null);

  protected async save() {
    this.busy.set(true);
    this.error.set(null);
    try {
      if (this.data.user) await this.api.updateUser(this.data.user.id, { name: this.name, email: this.email, timeZoneId: this.zone });
      else await this.api.createUser({ name: this.name, email: this.email, password: this.password, role: this.role, timeZoneId: this.zone });
      this.ref.close(true);
    } catch (e) {
      this.error.set(messageFor((e as Problem).code));
    } finally {
      this.busy.set(false);
    }
  }
}
