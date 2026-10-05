import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { WorkLogDto } from '../../core/models';
import { Clock, duration, hours, statusLabel } from '../../core/time';
import { Icon } from '../../ui/icon';
import { StatusPlate } from '../../ui/plates';

/** One log as a signed row; its history (who, when, why) unfolds underneath. */
@Component({
  selector: 'app-log-row',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, StatusPlate],
  template: `
    <div class="grid grid-cols-[1fr_auto] items-center gap-x-4 gap-y-2 py-3 sm:grid-cols-[7rem_1fr_auto_auto]">
      <div class="text-lg font-bold tabular-nums">{{ clock().time(log().startAt) }}–{{ clock().time(log().endAt) }}</div>
      <div class="order-3 col-span-2 flex min-w-0 items-center gap-2 text-sm text-ink-2 sm:order-none sm:col-span-1">
        <span class="font-semibold text-ink tabular-nums">{{ length() }}</span>
        @if (log().source === 'Manual') {
          <span class="inline-flex items-center gap-1" i18n-title="@@log.manualTitle" title="Ingresado a mano"><app-icon name="manual" [size]="15" /><ng-container i18n="@@log.manual">Manual</ng-container></span>
        }
        @if (log().note) { <span class="truncate">· {{ log().note }}</span> }
      </div>
      <app-status class="justify-self-end" [status]="log().status" />
      <div class="order-4 col-span-2 flex justify-end gap-1 sm:order-none sm:col-span-1">
        @if (editable()) {
          <button type="button" class="btn btn-quiet btn-sm" (click)="edit.emit(log())">
            <app-icon name="pencil" [size]="15" /><ng-container i18n="@@log.correct">Corregir</ng-container>
          </button>
        }
        <button type="button" class="btn btn-quiet btn-sm" (click)="open.set(!open())" [attr.aria-expanded]="open()">
          <ng-container i18n="@@log.history">Historial</ng-container>
          <app-icon name="chevronR" [size]="14" class="transition-transform duration-200" [class.rotate-90]="open()" />
        </button>
      </div>
    </div>
    @if (lastReason(); as r) {
      <p class="-mt-1 mb-3 text-sm"><span class="font-bold">{{ r.who }}:</span> {{ r.text }}</p>
    }
    @if (open()) {
      <ol class="mb-3 space-y-2 border-l border-rule pl-4 text-sm">
        @for (e of log().history; track $index) {
          <li>
            <span class="font-semibold">{{ clock().stamp(e.at) }}</span> · {{ e.actorName }} →
            <span class="font-bold">{{ label(e.to) }}</span>
            @if (e.reason) { <span class="text-ink-2"> — {{ e.reason }}</span> }
          </li>
        }
      </ol>
    }`,
})
export class LogRow {
  log = input.required<WorkLogDto>();
  clock = input.required<Clock>();
  editable = input(false);
  edit = output<WorkLogDto>();

  protected open = signal(false);
  protected label = statusLabel;
  protected length = computed(() => duration(hours(this.log().startAt, this.log().endAt)));

  /** Why it came back / was rejected: shown up front, not hidden in the history. */
  protected lastReason = computed(() => {
    const l = this.log();
    if (l.status !== 'NeedsRevision' && l.status !== 'Rejected') return null;
    const e = [...l.history].reverse().find(h => h.to === l.status && h.reason);
    return e ? { who: e.actorName, text: e.reason! } : null;
  });
}
