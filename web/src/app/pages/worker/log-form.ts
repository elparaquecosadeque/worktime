import { ChangeDetectionStrategy, Component, LOCALE_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { A11yModule } from '@angular/cdk/a11y';
import { Api } from '../../core/api';
import { messageFor } from '../../core/feedback';
import { Problem, WorkLogDto } from '../../core/models';
import { Clock } from '../../core/time';
import { Icon } from '../../ui/icon';

export interface LogFormData { zone: string; date: string; log?: WorkLogDto; }

/** Create a manual log, or correct one (reason required). Dialog because it protects a multi-field edit. */
@Component({
  selector: 'app-log-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, A11yModule, Icon],
  template: `
    <form class="w-[min(100vw-2rem,28rem)] rounded-[4px] bg-card p-6 shadow-lift" (ngSubmit)="save()" cdkTrapFocus [cdkTrapFocusAutoCapture]="true">
      <div class="flex items-start justify-between gap-4">
        <h2 class="text-xl font-extrabold">
          @if (data.log) { <span i18n="@@logForm.editTitle">Corregir registro</span> } @else { <span i18n="@@logForm.createTitle">Registrar horas</span> }
        </h2>
        <button type="button" class="-mr-2 -mt-1 p-2 text-ink-2 hover:text-ink" (click)="ref.close()" i18n-aria-label="@@common.close" aria-label="Cerrar">
          <app-icon name="cross" />
        </button>
      </div>
      @if (!data.log) {
        <p class="mt-1 text-sm text-ink-2" i18n="@@logForm.manualHint">Los registros manuales se marcan como tales para tu supervisor.</p>
      }

      <div class="mt-5 grid grid-cols-2 gap-4">
        <label class="field col-span-2">
          <span i18n="@@logForm.date">Día</span>
          <input class="input" type="date" name="date" required [(ngModel)]="date" [max]="today" />
        </label>
        <label class="field">
          <span i18n="@@logForm.start">Entrada</span>
          <input class="input" type="time" name="start" required [(ngModel)]="start" />
        </label>
        <label class="field">
          <span i18n="@@logForm.end">Salida</span>
          <input class="input" type="time" name="end" required [(ngModel)]="end" />
        </label>
        <label class="field col-span-2">
          <span i18n="@@logForm.note">Nota <span class="font-normal text-ink-3">(opcional)</span></span>
          <input class="input" name="note" maxlength="1000" [(ngModel)]="note" i18n-placeholder="@@logForm.notePh" placeholder="Proyecto, tarea, lo que ayude a revisar" />
        </label>
        @if (data.log) {
          <label class="field col-span-2">
            <span i18n="@@logForm.reason">Razón de la corrección</span>
            <textarea class="input min-h-20" name="reason" required maxlength="1000" [(ngModel)]="reason"
              i18n-placeholder="@@logForm.reasonPh" placeholder="Qué cambiaste y por qué"></textarea>
          </label>
        }
      </div>

      @if (error(); as message) { <p class="mt-4 text-sm font-semibold text-stop" role="alert">{{ message }}</p> }

      <div class="mt-6 flex justify-end gap-3">
        <button type="button" class="btn btn-quiet" (click)="ref.close()" i18n="@@common.cancel">Cancelar</button>
        <button type="submit" class="btn" [disabled]="busy() || !date || !start || !end || (!!data.log && !reason.trim())">
          @if (data.log) { <span i18n="@@logForm.submitEdit">Enviar corrección</span> } @else { <span i18n="@@logForm.submitCreate">Registrar</span> }
        </button>
      </div>
    </form>`,
})
export class LogForm {
  protected data = inject<LogFormData>(DIALOG_DATA);
  protected ref = inject<DialogRef<boolean>>(DialogRef);
  private api = inject(Api);
  private clock = new Clock(inject(LOCALE_ID), this.data.zone);

  protected today = this.clock.dateKey(new Date());
  protected date = this.data.log ? this.clock.dateKey(this.data.log.startAt) : this.data.date;
  protected start = this.data.log ? this.clock.toLocalInput(this.data.log.startAt).slice(11) : '08:00';
  protected end = this.data.log ? this.clock.toLocalInput(this.data.log.endAt).slice(11) : '17:00';
  protected note = this.data.log?.note ?? '';
  protected reason = '';
  protected busy = signal(false);
  protected error = signal<string | null>(null);

  protected async save() {
    this.busy.set(true);
    this.error.set(null);
    const body = {
      startAt: this.clock.toInstant(`${this.date}T${this.start}`),
      endAt: this.clock.toInstant(`${this.date}T${this.end}`),
      note: this.note.trim() || null,
    };
    try {
      if (this.data.log) await this.api.editLog(this.data.log.id, { ...body, reason: this.reason.trim() });
      else await this.api.createLog(body);
      this.ref.close(true);
    } catch (e) {
      this.error.set(messageFor((e as Problem).code));
    } finally {
      this.busy.set(false);
    }
  }
}
