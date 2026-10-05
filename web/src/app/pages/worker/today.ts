import { ChangeDetectionStrategy, Component, DestroyRef, LOCALE_ID, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Dialog } from '@angular/cdk/dialog';
import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { Toasts, messageFor } from '../../core/feedback';
import { MeDto, Perms, Problem, SupervisorRow, WorkLogDto } from '../../core/models';
import { Realtime } from '../../core/realtime';
import { Clock, duration, hours } from '../../core/time';
import { DayTrack, TrackSegment } from '../../ui/day-track';
import { Icon } from '../../ui/icon';
import { StationClock } from '../../ui/station-clock';
import { LogForm, LogFormData } from './log-form';
import { LogRow } from './log-row';

@Component({
  selector: 'app-today',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, RouterLink, Icon, StationClock, DayTrack, LogRow],
  template: `
    @if (me(); as me) {
      <div class="grid gap-8 lg:grid-cols-[minmax(0,5fr)_minmax(0,7fr)] lg:gap-12">
        <!-- The punch: the clock, the sign, the one action. -->
        <section class="flex flex-col items-center gap-6 sm:flex-row sm:items-center lg:flex-col lg:items-stretch" aria-labelledby="punch-h">
          <h1 id="punch-h" class="sr-only" i18n="@@today.punchHeading">Marcación</h1>
          <app-station-clock [zone]="zone()" class="size-52 shrink-0 sm:size-56 lg:mx-auto lg:size-72" [label]="clockLabel" />
          <div class="flex w-full flex-col gap-3">
            @if (me.workingSince; as since) {
              <div class="flex items-center gap-3 rounded-[2px] bg-go px-4 pb-2.5 pt-3 text-white">
                <span class="size-2.5 shrink-0 rounded-full bg-white motion-safe:animate-pulse"></span>
                <span class="text-2xl font-extrabold tabular-nums leading-tight" i18n="@@today.workingSince">Trabajando desde {{ clock().time(since) }}</span>
              </div>
              <button type="button" class="btn btn-stop h-16 text-lg" (click)="punch('out')" [disabled]="busy()">
                <app-icon name="punch" [size]="22" /><ng-container i18n="@@today.punchOut">Marcar salida</ng-container>
              </button>
            } @else {
              <div class="rounded-[2px] px-4 pb-2.5 pt-3 shadow-[inset_0_0_0_1px_var(--color-rule)]">
                <span class="text-2xl font-extrabold leading-tight" i18n="@@today.notWorking">Fuera de turno</span>
              </div>
              <button type="button" class="btn btn-go h-16 text-lg" (click)="punch('in')" [disabled]="busy()">
                <app-icon name="punch" [size]="22" /><ng-container i18n="@@today.punchIn">Marcar entrada</ng-container>
              </button>
            }
          </div>
        </section>

        <!-- The day: track, total, logs. -->
        <section aria-labelledby="day-h" class="min-w-0">
          <div class="flex flex-wrap items-end justify-between gap-x-6 gap-y-1">
            <h2 id="day-h" class="text-2xl font-extrabold first-letter:uppercase sm:text-3xl">{{ clock().dayLong(now()) }}</h2>
            <p class="flex items-baseline gap-1.5 text-sm text-ink-2"><span class="text-xl font-extrabold text-ink tabular-nums">{{ todayTotal() }}</span><span i18n="@@today.total">hoy</span></p>
          </div>
          <app-day-track class="mt-4 block" [clock]="clock()" [segments]="segments()" [now]="now()" [label]="trackLabel" />

          <div class="mt-6 divide-y divide-rule border-y border-rule">
            @if (me.workingSince; as since) {
              <!-- The open shift is today's first row, not an empty state. -->
              <div class="flex items-center justify-between gap-4 py-3">
                <div class="text-lg font-bold tabular-nums">{{ clock().time(since) }}–<span class="text-ink-2" i18n="@@today.ongoing">en curso</span></div>
                <span class="plate bg-go text-white"><span class="size-2 rounded-full bg-white"></span><span i18n="@@today.workingPlate">Trabajando</span></span>
              </div>
            }
            @for (log of todayLogs(); track log.id) {
              <app-log-row [log]="log" [clock]="clock()" [editable]="log.status === 'Pending' || log.status === 'NeedsRevision'" (edit)="openForm($event)" />
            } @empty {
              @if (!me.workingSince) {
                <p class="py-6 text-ink-2" i18n="@@today.empty">Aún no hay registros hoy. Al marcar salida, tu turno aparece aquí como pendiente de aprobación.</p>
              }
            }
          </div>
          <p class="mt-3 text-sm text-ink-2">
            <ng-container i18n="@@today.forgot">¿Olvidaste marcar?</ng-container>
            <button type="button" class="ml-1 font-bold text-sign underline underline-offset-4" (click)="openForm()" i18n="@@today.addManual">Registra horas a mano</button>
            · <a routerLink="/mes" class="font-bold" i18n="@@today.seeMonth">Ver el mes</a>
          </p>

          <!-- Supervisor sign. -->
          <div class="mt-10">
            @if (me.user.supervisorName; as name) {
              <p class="flex items-center gap-2 text-lg"><app-icon name="person" /><span i18n="@@today.supervisorLine">Supervisor: <span class="font-bold">{{ name }}</span></span></p>
            } @else if (me.pendingRequest; as req) {
              <div class="mt-2 rounded-[2px] bg-wait px-4 pb-3 pt-3.5 text-ink">
                <p class="font-bold" i18n="@@today.requestSent">Sin supervisor · solicitud enviada el {{ clock().stamp(req.createdAt) }}</p>
                <p class="mt-1 text-sm" i18n="@@today.requestSentHint">Un admin te asignará uno. Mientras tanto puedes seguir marcando y registrando horas.</p>
              </div>
            } @else {
              <form class="mt-2 space-y-3 rounded-[2px] p-4 shadow-[inset_0_0_0_1px_var(--color-rule)]" (ngSubmit)="requestSupervisor()">
                <p class="font-bold" i18n="@@today.noSupervisor">No tienes supervisor. Tus registros quedan en la bandeja del admin.</p>
                @if (canRequest()) {
                  <div class="grid gap-3 sm:grid-cols-2">
                    <label class="field">
                      <span i18n="@@today.preferred">Supervisor sugerido <span class="font-normal text-ink-3">(opcional)</span></span>
                      <select class="input" name="preferred" [(ngModel)]="preferred">
                        <option [ngValue]="null" i18n="@@today.noPreference">Sin preferencia</option>
                        @for (s of supervisors(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
                      </select>
                    </label>
                    <label class="field">
                      <span i18n="@@today.requestNote">Nota para el admin</span>
                      <input class="input" name="note" maxlength="1000" [(ngModel)]="requestNote" />
                    </label>
                  </div>
                  <button type="submit" class="btn" [disabled]="busy()"><app-icon name="hand" [size]="18" /><ng-container i18n="@@today.requestCta">Pedir un supervisor</ng-container></button>
                }
              </form>
            }
          </div>
        </section>
      </div>
    } @else {
      <div class="grid gap-8 lg:grid-cols-[5fr_7fr]">
        <div class="skeleton mx-auto size-72 rounded-full"></div>
        <div class="space-y-4"><div class="skeleton h-9 w-2/3"></div><div class="skeleton h-10"></div><div class="skeleton h-40"></div></div>
      </div>
    }`,
})
export class TodayPage {
  private api = inject(Api);
  private auth = inject(Auth);
  private toasts = inject(Toasts);
  private dialog = inject(Dialog);
  private locale = inject(LOCALE_ID);

  protected me = signal<MeDto | null>(null);
  protected logs = signal<WorkLogDto[]>([]);
  protected supervisors = signal<SupervisorRow[]>([]);
  protected busy = signal(false);
  protected now = signal(new Date());
  protected preferred: string | null = null;
  protected requestNote = '';
  protected clockLabel = $localize`:@@today.clockLabel:Reloj con tu hora local`;
  protected trackLabel = $localize`:@@today.trackLabel:Tus registros de hoy sobre las 24 horas`;

  protected zone = computed(() => this.me()?.user.timeZoneId ?? 'UTC');
  protected clock = computed(() => new Clock(this.locale, this.zone()));
  protected canRequest = computed(() => this.auth.has(Perms.AssignmentsRequest));

  protected todayLogs = computed(() => {
    const key = this.clock().dateKey(this.now());
    return this.logs().filter(l => this.clock().dateKey(l.startAt) === key);
  });

  protected segments = computed<TrackSegment[]>(() => {
    const s: TrackSegment[] = this.todayLogs().map(l => ({ start: l.startAt, end: l.endAt, status: l.status }));
    const since = this.me()?.workingSince;
    if (since) s.push({ start: this.clock().dateKey(since) === this.clock().dateKey(this.now()) ? since : this.now().toISOString(), end: null, status: 'Open' });
    return s;
  });

  protected todayTotal = computed(() => {
    const closed = this.todayLogs().filter(l => l.status !== 'Rejected').reduce((h, l) => h + hours(l.startAt, l.endAt), 0);
    const since = this.me()?.workingSince;
    return duration(closed + (since ? hours(since, this.now().toISOString()) : 0));
  });

  constructor() {
    const timer = setInterval(() => this.now.set(new Date()), 15_000);
    inject(DestroyRef).onDestroy(() => clearInterval(timer));

    const realtime = inject(Realtime);
    const mine = (p: { workerId?: string }) => p?.workerId === this.auth.user()?.userId;
    realtime.on(['PunchChanged', 'WorkLogChanged', 'WorkLogStatusChanged', 'AssignmentChanged', 'AssignmentResolved'], e => {
      if (!mine(e.payload)) return;
      if (e.name === 'WorkLogStatusChanged') this.patchStatus(e.payload);
      void this.load();
    });
    realtime.onResync(() => void this.load());

    void this.load();
    this.api.supervisors().then(s => this.supervisors.set(s), () => undefined);
  }

  private async load() {
    try {
      const me = await this.api.me();
      this.me.set(me);
      const p = new Clock(this.locale, me.user.timeZoneId).parts(new Date());
      this.logs.set(await this.api.myMonth(p.year, p.month));
      this.now.set(new Date());
    } catch (e) {
      this.toasts.problem(e as Problem);
    }
  }

  private patchStatus(p: { workLogId: string; status: WorkLogDto['status']; actorName: string; reason: string | null }) {
    const msg = p.status === 'Approved' ? $localize`:@@today.approvedBy:${p.actorName}:name: aprobó un registro tuyo.`
      : p.status === 'Rejected' ? $localize`:@@today.rejectedBy:${p.actorName}:name: rechazó un registro tuyo.`
      : $localize`:@@today.revisionBy:${p.actorName}:name: te pidió corregir un registro.`;
    this.toasts.show(msg, p.status === 'Approved' ? 'ok' : 'info');
  }

  protected async punch(kind: 'in' | 'out') {
    this.busy.set(true);
    try {
      if (kind === 'in') await this.api.punchIn(); else await this.api.punchOut();
      await this.load();
    } catch (e) {
      this.toasts.problem(e as Problem);
      await this.load();
    } finally {
      this.busy.set(false);
    }
  }

  protected openForm(log?: WorkLogDto) {
    const data: LogFormData = { zone: this.zone(), date: this.clock().dateKey(new Date()), log };
    this.dialog.open<boolean>(LogForm, { data, backdropClass: 'cdk-overlay-dark-backdrop' }).closed.subscribe(saved => {
      if (!saved) return;
      this.toasts.show(log ? $localize`:@@today.correctionSent:Corrección enviada. El registro vuelve a pendiente.` : $localize`:@@today.logCreated:Registro creado como pendiente.`, 'ok');
      void this.load();
    });
  }

  protected async requestSupervisor() {
    this.busy.set(true);
    try {
      await this.api.requestAssignment(this.requestNote.trim() || null, this.preferred);
      this.toasts.show($localize`:@@today.requestDone:Solicitud enviada al admin.`, 'ok');
      await this.load();
    } catch (e) {
      this.toasts.show(messageFor((e as Problem).code), 'error');
    } finally {
      this.busy.set(false);
    }
  }
}
