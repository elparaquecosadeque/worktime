import { ChangeDetectionStrategy, Component, LOCALE_ID, computed, inject, signal } from '@angular/core';
import { Dialog } from '@angular/cdk/dialog';
import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { Toasts } from '../../core/feedback';
import { Problem, WorkLogDto, WorkLogStatus } from '../../core/models';
import { Realtime } from '../../core/realtime';
import { Clock, duration, hours, pad, statusLabel } from '../../core/time';
import { Icon } from '../../ui/icon';
import { STATUS_STYLE } from '../../ui/plates';
import { LogForm, LogFormData } from './log-form';
import { LogRow } from './log-row';

interface Day { key: string; n: number; inMonth: boolean; future: boolean; logs: WorkLogDto[]; total: number; parts: { status: WorkLogStatus; pct: number }[]; }

const ORDER: WorkLogStatus[] = ['Approved', 'Pending', 'NeedsRevision', 'Rejected'];
const BAR: Record<WorkLogStatus, string> = { Approved: 'bg-go', Pending: 'bg-wait', NeedsRevision: 'bg-back', Rejected: 'bg-stop' };
/** A full bar is a 12 h day: bar length is hours, never a relative score. */
const FULL_DAY_HOURS = 12;

@Component({
  selector: 'app-month',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, LogRow],
  template: `
    <div class="flex flex-wrap items-center justify-between gap-4">
      <div class="flex items-center gap-2">
        <button type="button" class="btn btn-quiet btn-sm !px-2" (click)="shift(-1)" i18n-aria-label="@@month.prev" aria-label="Mes anterior"><app-icon name="chevronL" [size]="18" /></button>
        <h1 class="min-w-48 text-center text-2xl font-extrabold first-letter:uppercase sm:text-3xl">{{ clock().monthTitle(year(), month()) }}</h1>
        <button type="button" class="btn btn-quiet btn-sm !px-2" (click)="shift(1)" [disabled]="isCurrentMonth()" i18n-aria-label="@@month.next" aria-label="Mes siguiente"><app-icon name="chevronR" [size]="18" /></button>
      </div>
      <p class="text-sm font-semibold text-ink-2" i18n="@@month.rangeHeader">{{ logs().length }} registros · {{ totalAll() }}</p>
    </div>

    <!-- Month totals, one plate per state, hours in tabular figures. -->
    <dl class="mt-5 grid grid-cols-2 gap-2 sm:grid-cols-4">
      @for (s of order; track s) {
        <div class="flex items-center justify-between gap-3 rounded-[2px] px-3 pb-2 pt-2.5" [class]="plate(s)">
          <dt class="text-xs font-bold uppercase tracking-[0.06em]">{{ label(s) }}</dt>
          <dd class="text-lg font-extrabold tabular-nums">{{ totals()[s] }}</dd>
        </div>
      }
    </dl>

    <div class="mt-6 grid gap-8 lg:grid-cols-[minmax(0,7fr)_minmax(0,5fr)]">
      <div>
        <div class="grid grid-cols-7 gap-px text-center text-xs font-bold uppercase tracking-[0.06em] text-ink-3" aria-hidden="true">
          @for (d of weekdays(); track $index) { <div class="pb-2">{{ d }}</div> }
        </div>
        <div class="grid grid-cols-7 gap-px overflow-hidden rounded-[2px] bg-rule shadow-[0_0_0_1px_var(--color-rule)]" role="grid" [attr.aria-label]="clock().monthTitle(year(), month())">
          @for (d of days(); track d.key) {
            <button type="button" class="day" role="gridcell" [class.is-out]="!d.inMonth" [class.is-selected]="d.key === selected()"
              [class.is-today]="d.key === todayKey()" [disabled]="!d.inMonth" (click)="selected.set(d.key)"
              [attr.aria-label]="d.key + ' · ' + fmt(d.total)" [attr.aria-pressed]="d.key === selected()">
              <span class="text-sm font-bold tabular-nums">{{ d.n }}</span>
              @if (d.total > 0) {
                <span class="mt-auto hidden text-xs font-bold tabular-nums sm:block">{{ fmt(d.total) }}</span>
                <span class="mt-1 flex h-1.5 w-full overflow-hidden bg-panel sm:mt-1">
                  @for (p of d.parts; track p.status) { <span [class]="bar(p.status)" [style.width.%]="p.pct"></span> }
                </span>
              }
            </button>
          }
        </div>
      </div>

      <section aria-live="polite" class="min-w-0">
        @if (selectedDay(); as day) {
          <div class="flex items-end justify-between gap-4">
            <h2 class="text-xl font-extrabold first-letter:uppercase">{{ clock().dayLong(day.logs[0]?.startAt ?? noon(day.key)) }}</h2>
            @if (!day.future && withinWindow(day.key)) {
              <button type="button" class="btn btn-sm" (click)="openForm(day.key)"><app-icon name="plus" [size]="16" /><ng-container i18n="@@month.add">Registrar</ng-container></button>
            }
          </div>
          <div class="mt-3 divide-y divide-rule border-y border-rule">
            @for (log of day.logs; track log.id) {
              <app-log-row [log]="log" [clock]="clock()" [editable]="log.status === 'Pending' || log.status === 'NeedsRevision'" (edit)="openForm(day.key, $event)" />
            } @empty {
              <p class="py-6 text-ink-2" i18n="@@month.dayEmpty">Sin registros este día.</p>
            }
          </div>
        } @else {
          <p class="rounded-[2px] p-5 text-ink-2 shadow-[inset_0_0_0_1px_var(--color-rule)]" i18n="@@month.pickDay">Elige un día para ver sus registros, el estado de cada uno y quién lo decidió.</p>
        }
      </section>
    </div>`,
  styles: `
    .day { display: flex; flex-direction: column; align-items: flex-start; gap: .125rem; min-height: 4.25rem; padding: .5rem .5rem .5rem;
      background: var(--color-card); text-align: left; cursor: pointer; transition: background-color 150ms var(--ease-out-expo); }
    @media (min-width: 640px) { .day { min-height: 5.5rem; } }
    .day:hover:not(:disabled) { background: #fff; }
    .day.is-out { background: var(--color-ground); color: var(--color-ink-3); cursor: default; }
    .day.is-today { box-shadow: inset 0 3px 0 var(--color-stop); }
    .day.is-selected { background: var(--color-ink); color: var(--color-ground); }
    .day.is-selected .bg-panel { background: rgb(255 255 255 / .2); }
  `,
})
export class MonthPage {
  private api = inject(Api);
  private auth = inject(Auth);
  private toasts = inject(Toasts);
  private dialog = inject(Dialog);
  private locale = inject(LOCALE_ID);

  private zone = signal('UTC');
  protected clock = computed(() => new Clock(this.locale, this.zone()));
  protected year = signal(0);
  protected month = signal(0);
  protected logs = signal<WorkLogDto[]>([]);
  protected selected = signal<string | null>(null);
  protected order = ORDER;
  protected label = statusLabel;
  protected fmt = duration;

  protected todayKey = computed(() => this.clock().dateKey(new Date()));
  protected isCurrentMonth = computed(() => { const p = this.clock().parts(new Date()); return p.year === this.year() && p.month === this.month(); });

  protected weekdays = computed(() => {
    const f = new Intl.DateTimeFormat(this.locale, { weekday: 'short', timeZone: 'UTC' });
    return Array.from({ length: 7 }, (_, i) => f.format(Date.UTC(2024, 0, 1 + i))); // 2024-01-01 is a Monday
  });

  protected days = computed<Day[]>(() => {
    const y = this.year(), m = this.month();
    if (!y) return [];
    const byKey = new Map<string, WorkLogDto[]>();
    for (const l of this.logs()) {
      const k = this.clock().dateKey(l.startAt);
      byKey.set(k, [...(byKey.get(k) ?? []), l]);
    }
    const first = new Date(Date.UTC(y, m - 1, 1));
    const lead = (first.getUTCDay() + 6) % 7; // Monday-first
    const count = new Date(Date.UTC(y, m, 0)).getUTCDate();
    const cells = Math.ceil((lead + count) / 7) * 7;
    const today = this.todayKey();
    return Array.from({ length: cells }, (_, i) => {
      const date = new Date(Date.UTC(y, m - 1, 1 - lead + i));
      const key = `${date.getUTCFullYear()}-${pad(date.getUTCMonth() + 1)}-${pad(date.getUTCDate())}`;
      const logs = byKey.get(key) ?? [];
      const byStatus = ORDER.map(status => ({ status, h: logs.filter(l => l.status === status).reduce((s, l) => s + hours(l.startAt, l.endAt), 0) }));
      const total = byStatus.reduce((s, x) => s + x.h, 0);
      return {
        key, n: date.getUTCDate(), inMonth: date.getUTCMonth() === m - 1, future: key > today, logs, total,
        parts: byStatus.filter(x => x.h > 0).map(x => ({ status: x.status, pct: Math.min(100, (x.h / FULL_DAY_HOURS) * 100) })),
      };
    });
  });

  protected selectedDay = computed(() => this.days().find(d => d.key === this.selected()) ?? null);

  protected totals = computed(() => Object.fromEntries(ORDER.map(s =>
    [s, duration(this.logs().filter(l => l.status === s).reduce((h, l) => h + hours(l.startAt, l.endAt), 0))])) as Record<WorkLogStatus, string>);

  protected totalAll = computed(() => duration(this.logs().filter(l => l.status !== 'Rejected').reduce((h, l) => h + hours(l.startAt, l.endAt), 0)));

  constructor() {
    const realtime = inject(Realtime);
    realtime.on(['WorkLogChanged', 'WorkLogStatusChanged', 'PunchChanged'], e => {
      if (e.payload?.workerId === this.auth.user()?.userId) void this.load();
    });
    realtime.onResync(() => void this.load());
    void this.init();
  }

  private async init() {
    try {
      const me = await this.api.me();
      this.zone.set(me.user.timeZoneId);
      const p = this.clock().parts(new Date());
      this.year.set(p.year);
      this.month.set(p.month);
      this.selected.set(this.todayKey());
      await this.load();
    } catch (e) {
      this.toasts.problem(e as Problem);
    }
  }

  private async load() {
    if (!this.year()) return;
    try {
      this.logs.set(await this.api.myMonth(this.year(), this.month()));
    } catch (e) {
      this.toasts.problem(e as Problem);
    }
  }

  protected shift(delta: number) {
    const d = new Date(Date.UTC(this.year(), this.month() - 1 + delta, 1));
    this.year.set(d.getUTCFullYear());
    this.month.set(d.getUTCMonth() + 1);
    this.selected.set(null);
    this.logs.set([]);
    void this.load();
  }

  protected plate = (s: WorkLogStatus) => STATUS_STYLE[s].cls;
  protected bar = (s: WorkLogStatus) => BAR[s] + ' h-full';
  protected noon = (key: string) => `${key}T17:00:00Z`;

  protected withinWindow(key: string) {
    const limit = new Date(Date.now() - 31 * 86_400_000);
    return key >= this.clock().dateKey(limit);
  }

  protected openForm(date: string, log?: WorkLogDto) {
    const data: LogFormData = { zone: this.zone(), date, log };
    this.dialog.open<boolean>(LogForm, { data, backdropClass: 'cdk-overlay-dark-backdrop' }).closed.subscribe(saved => {
      if (!saved) return;
      this.toasts.show(log ? $localize`:@@today.correctionSent:Corrección enviada. El registro vuelve a pendiente.` : $localize`:@@today.logCreated:Registro creado como pendiente.`, 'ok');
      void this.load();
    });
  }
}
