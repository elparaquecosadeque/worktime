import { ChangeDetectionStrategy, Component, LOCALE_ID, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { Toasts, messageFor } from '../../core/feedback';
import { Decision, Perms, Problem, WorkLogDto, WorkLogStatus } from '../../core/models';
import { Realtime } from '../../core/realtime';
import { Clock, duration, hours, statusLabel } from '../../core/time';
import { Icon } from '../../ui/icon';
import { STATUS_STYLE } from '../../ui/plates';

interface RowState { asking?: Exclude<Decision, 'Approve'>; reason?: string; busy?: boolean; error?: string; decided?: { status: WorkLogStatus; by: string }; }

@Component({
  selector: 'app-inbox',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, Icon],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-4">
      <div>
        <h1 class="text-2xl font-extrabold sm:text-3xl" i18n="@@inbox.title">Bandeja de aprobación</h1>
        <p class="mt-1 text-sm font-semibold text-ink-2">
          @if (range(); as r) { <ng-container i18n="@@inbox.range">{{ items().length }} por decidir · {{ r }}</ng-container> }
          @else { <ng-container i18n="@@inbox.rangeEmpty">Nada por decidir</ng-container> }
        </p>
      </div>
      <div class="flex flex-wrap items-center gap-3">
        @if (all()) {
          <div class="flex rounded-[2px] p-0.5 shadow-[inset_0_0_0_1px_var(--color-rule)]" role="group" i18n-aria-label="@@inbox.scope" aria-label="Alcance">
            <button type="button" class="seg" [class.is-on]="!orphans()" (click)="setOrphans(false)" i18n="@@inbox.scopeAll">Todos</button>
            <button type="button" class="seg" [class.is-on]="orphans()" (click)="setOrphans(true)" i18n="@@inbox.scopeOrphans">Sin supervisor</button>
          </div>
        }
        <label class="sr-only" for="worker-filter" i18n="@@inbox.filterLabel">Filtrar por trabajador</label>
        <select id="worker-filter" class="input !w-auto" [ngModel]="workerFilter()" (ngModelChange)="workerFilter.set($event)">
          <option [ngValue]="null" i18n="@@inbox.everyone">Todos los trabajadores</option>
          @for (w of workers(); track w.id) { <option [ngValue]="w.id">{{ w.name }}</option> }
        </select>
      </div>
    </div>

    <!-- Batch bar: appears with a selection; partial success is reported per log. -->
    @if (selected().size) {
      <div class="sticky top-16 z-20 mt-5 flex flex-wrap items-center gap-3 rounded-[2px] bg-ink px-4 py-3 text-ground shadow-lift">
        <span class="font-bold tabular-nums" i18n="@@inbox.selected">{{ selected().size }} seleccionados</span>
        <input class="input !min-h-9 max-w-xs flex-1 !bg-ground" [(ngModel)]="batchReason" i18n-placeholder="@@inbox.batchReasonPh" placeholder="Razón (para revisión o rechazo)" />
        <div class="ml-auto flex flex-wrap gap-2">
          <button type="button" class="btn btn-go btn-sm" (click)="batch('Approve')" [disabled]="batching()"><app-icon name="check" [size]="16" /><ng-container i18n="@@inbox.approveAll">Aprobar</ng-container></button>
          <button type="button" class="btn btn-sm !bg-back" (click)="batch('RequestRevision')" [disabled]="batching() || !batchReason.trim()"><app-icon name="back" [size]="16" /><ng-container i18n="@@inbox.revisionAll">Pedir corrección</ng-container></button>
          <button type="button" class="btn btn-stop btn-sm" (click)="batch('Reject')" [disabled]="batching() || !batchReason.trim()"><app-icon name="cross" [size]="16" /><ng-container i18n="@@inbox.rejectAll">Rechazar</ng-container></button>
          <button type="button" class="btn btn-sm !bg-transparent !text-ground underline" (click)="clearSelection()" i18n="@@inbox.clear">Quitar selección</button>
        </div>
      </div>
    }

    @if (loaded()) {
      <ul class="mt-5 divide-y divide-rule border-y border-rule">
        @if (visible().length) {
          <li class="flex items-center gap-3 py-2 text-sm text-ink-2">
            <input type="checkbox" class="size-4" [checked]="allSelected()" (change)="toggleAll()" id="all" />
            <label for="all" class="font-semibold" i18n="@@inbox.selectAll">Seleccionar todo lo visible</label>
          </li>
        }
        @for (log of visible(); track log.id) {
          @let st = state()[log.id] ?? {};
          <li class="row grid grid-cols-[auto_1fr] gap-x-3 gap-y-2 py-4 md:grid-cols-[auto_minmax(0,14rem)_minmax(0,1fr)_auto]" [class.is-gone]="st.decided">
            <input type="checkbox" class="mt-1.5 size-4" [checked]="selected().has(log.id)" (change)="toggle(log.id)" [disabled]="!!st.decided"
              [attr.aria-label]="log.workerName + ' ' + clockOf(log).day(log.startAt)" />
            <div>
              <div class="font-bold">{{ log.workerName }}</div>
              <div class="text-sm text-ink-2 first-letter:uppercase">{{ clockOf(log).day(log.startAt) }}</div>
            </div>
            <div class="col-start-2 min-w-0 md:col-start-auto">
              <div class="flex flex-wrap items-baseline gap-x-3">
                <span class="text-lg font-bold tabular-nums">{{ clockOf(log).time(log.startAt) }}–{{ clockOf(log).time(log.endAt) }}</span>
                <span class="font-semibold tabular-nums">{{ len(log) }}</span>
                @if (log.source === 'Manual') { <span class="plate text-ink shadow-[inset_0_0_0_1px_var(--color-ink)]"><app-icon name="manual" [size]="13" /><ng-container i18n="@@log.manual">Manual</ng-container></span> }
              </div>
              @if (log.note) { <p class="mt-1 text-sm text-ink-2">{{ log.note }}</p> }
              @if (lastEdit(log); as e) { <p class="mt-1 text-sm"><span class="font-bold" i18n="@@inbox.corrected">Corregido:</span> {{ e }}</p> }
            </div>
            <div class="col-span-2 md:col-span-1 md:justify-self-end">
              @if (st.decided; as d) {
                <span class="plate" [class]="plate(d.status)" i18n="@@inbox.decidedBy">{{ label(d.status) }} por {{ d.by }}</span>
              } @else if (st.asking) {
                <form class="flex flex-wrap items-center gap-2" (ngSubmit)="decide(log, st.asking, st.reason ?? '')">
                  <input class="input !min-h-9 w-56" [ngModel]="st.reason" (ngModelChange)="setState(log.id, { reason: $event })" name="reason" required
                    [attr.aria-label]="reasonLabel" [placeholder]="st.asking === 'Reject' ? rejectPh : revisionPh" />
                  <button type="submit" class="btn btn-sm" [class]="st.asking === 'Reject' ? 'btn-stop' : '!bg-back'" [disabled]="st.busy || !(st.reason ?? '').trim()">
                    @if (st.asking === 'Reject') { <ng-container i18n="@@inbox.confirmReject">Rechazar</ng-container> } @else { <ng-container i18n="@@inbox.confirmRevision">Devolver</ng-container> }
                  </button>
                  <button type="button" class="btn btn-quiet btn-sm" (click)="setState(log.id, { asking: undefined })" i18n="@@common.cancel">Cancelar</button>
                </form>
              } @else {
                <div class="flex flex-wrap gap-2">
                  <button type="button" class="btn btn-go btn-sm" (click)="decide(log, 'Approve', null)" [disabled]="st.busy"><app-icon name="check" [size]="16" /><ng-container i18n="@@inbox.approve">Aprobar</ng-container></button>
                  <button type="button" class="btn btn-quiet btn-sm" (click)="setState(log.id, { asking: 'RequestRevision' })" i18n="@@inbox.revision">Corrección</button>
                  <button type="button" class="btn btn-quiet btn-sm" (click)="setState(log.id, { asking: 'Reject' })" i18n="@@inbox.reject">Rechazar</button>
                </div>
              }
              @if (st.error) { <p class="mt-2 text-sm font-semibold text-stop" role="alert">{{ st.error }}</p> }
            </div>
          </li>
        } @empty {
          <li class="py-10">
            <p class="text-lg font-bold" i18n="@@inbox.emptyTitle">Todo decidido.</p>
            <p class="mt-1 text-ink-2" i18n="@@inbox.emptyBody">Cuando alguien de tu equipo marque salida o corrija un registro, aparecerá aquí al instante.</p>
          </li>
        }
      </ul>
    } @else {
      <div class="mt-5 space-y-px">@for (i of [1, 2, 3, 4]; track i) { <div class="skeleton h-20"></div> }</div>
    }`,
  styles: `
    .seg { padding: .375rem .75rem .25rem; font-size: .875rem; font-weight: 700; border-radius: 2px; color: var(--color-ink-2); }
    .seg.is-on { background: var(--color-ink); color: var(--color-ground); }
    .row { transition: opacity 400ms var(--ease-out-expo); }
    .row.is-gone { opacity: .55; }
  `,
})
export class InboxPage {
  /** Query params (component input binding): ?huerfanos=1, ?trabajador=<id>. */
  huerfanos = input<string>();
  trabajador = input<string>();

  private api = inject(Api);
  private auth = inject(Auth);
  private toasts = inject(Toasts);
  private router = inject(Router);
  private locale = inject(LOCALE_ID);
  private clocks = new Map<string, Clock>();
  private refetch: ReturnType<typeof setTimeout> | undefined;

  protected items = signal<WorkLogDto[]>([]);
  protected loaded = signal(false);
  protected state = signal<Record<string, RowState>>({});
  protected selected = signal(new Set<string>());
  protected batching = signal(false);
  protected batchReason = '';
  protected label = statusLabel;
  protected reasonLabel = $localize`:@@inbox.reasonLabel:Razón`;
  protected rejectPh = $localize`:@@inbox.rejectPh:Por qué se rechaza`;
  protected revisionPh = $localize`:@@inbox.revisionPh:Qué debe corregir`;

  protected all = computed(() => this.auth.has(Perms.MonitorAll));
  protected orphans = computed(() => this.huerfanos() === '1' && this.all());
  /** Starts from ?trabajador= and then follows the select. */
  protected workerFilter = linkedSignal<string | null>(() => this.trabajador() ?? null);

  protected workers = computed(() => {
    const seen = new Map<string, string>();
    for (const l of this.items()) seen.set(l.workerId, l.workerName);
    return [...seen].map(([id, name]) => ({ id, name })).sort((a, b) => a.name.localeCompare(b.name));
  });

  protected visible = computed(() => {
    const w = this.workerFilter();
    return w ? this.items().filter(l => l.workerId === w) : this.items();
  });

  protected allSelected = computed(() => this.visible().length > 0 && this.visible().every(l => this.selected().has(l.id)));

  protected range = computed(() => {
    const list = this.visible();
    if (!list.length) return null;
    const c = this.clockOf(list[0]);
    return `${c.day(list[0].startAt)} – ${c.day(list[list.length - 1].startAt)}`;
  });

  constructor() {
    const realtime = inject(Realtime);
    realtime.on(['WorkLogStatusChanged'], e => {
      const p = e.payload as { workLogId: string; status: WorkLogStatus; actorName: string; actorId: string };
      if (!this.items().some(l => l.id === p.workLogId) || p.status === 'Pending') return;
      this.markDecided(p.workLogId, p.status, p.actorId === this.auth.user()?.userId ? $localize`:@@inbox.you:ti` : p.actorName);
    });
    realtime.on(['WorkLogChanged', 'AssignmentChanged'], () => this.scheduleRefetch());
    realtime.onResync(() => void this.load());
    queueMicrotask(() => void this.load());
  }

  protected clockOf(l: WorkLogDto) {
    let c = this.clocks.get(l.workerTimeZoneId);
    if (!c) this.clocks.set(l.workerTimeZoneId, (c = new Clock(this.locale, l.workerTimeZoneId)));
    return c;
  }

  protected len = (l: WorkLogDto) => duration(hours(l.startAt, l.endAt));
  protected plate = (s: WorkLogStatus) => STATUS_STYLE[s].cls;

  protected lastEdit(l: WorkLogDto) {
    const e = [...l.history].reverse().find(h => h.from && h.to === 'Pending' && h.reason);
    return e?.reason ?? null;
  }

  protected setState(id: string, patch: Partial<RowState>) {
    this.state.update(s => ({ ...s, [id]: { ...s[id], ...patch } }));
  }

  protected toggle(id: string) {
    this.selected.update(s => { const n = new Set(s); n.has(id) ? n.delete(id) : n.add(id); return n; });
  }

  protected clearSelection() { this.selected.set(new Set()); }

  protected toggleAll() {
    const all = this.allSelected();
    this.selected.set(all ? new Set() : new Set(this.visible().filter(l => !this.state()[l.id]?.decided).map(l => l.id)));
  }

  protected setOrphans(on: boolean) {
    void this.router.navigate([], { queryParams: { huerfanos: on ? 1 : null }, queryParamsHandling: 'merge' }).then(() => this.load());
  }

  protected async decide(log: WorkLogDto, decision: Decision, reason: string | null) {
    this.setState(log.id, { busy: true, error: undefined });
    try {
      await this.api.decide(log.id, decision, reason?.trim() || null);
      const status: WorkLogStatus = decision === 'Approve' ? 'Approved' : decision === 'Reject' ? 'Rejected' : 'NeedsRevision';
      this.markDecided(log.id, status, $localize`:@@inbox.you:ti`);
    } catch (e) {
      const p = e as Problem;
      // 409: someone else won the race. The event with their name usually lands first; otherwise say it plainly.
      this.setState(log.id, { busy: false, asking: undefined, error: messageFor(p.code) });
      if (p.status === 409 || p.status === 403) this.scheduleRefetch();
    }
  }

  protected async batch(decision: Decision) {
    const ids = [...this.selected()];
    this.batching.set(true);
    try {
      const results = await this.api.decideBatch(ids, decision, this.batchReason.trim() || null);
      const ok = results.filter(r => r.outcome === 'Ok');
      const status: WorkLogStatus = decision === 'Approve' ? 'Approved' : decision === 'Reject' ? 'Rejected' : 'NeedsRevision';
      for (const r of ok) this.markDecided(r.workLogId, status, $localize`:@@inbox.you:ti`);
      for (const r of results.filter(x => x.outcome !== 'Ok')) this.setState(r.workLogId, { error: messageFor(r.code ?? '') });
      const failed = results.length - ok.length;
      this.toasts.show(failed
        ? $localize`:@@inbox.batchPartial:${ok.length}:ok: decididos · ${failed}:failed: ya los decidió otra persona o cambiaron.`
        : $localize`:@@inbox.batchOk:${ok.length}:ok: registros decididos.`, failed ? 'info' : 'ok');
      this.selected.set(new Set());
      this.batchReason = '';
    } catch (e) {
      this.toasts.problem(e as Problem);
    } finally {
      this.batching.set(false);
    }
  }

  /** Decided rows stay visible a moment with who decided, then leave the inbox. */
  private markDecided(id: string, status: WorkLogStatus, by: string) {
    this.setState(id, { decided: { status, by }, busy: false, asking: undefined });
    this.selected.update(s => { const n = new Set(s); n.delete(id); return n; });
    setTimeout(() => this.items.update(list => list.filter(l => l.id !== id)), 4000);
  }

  private scheduleRefetch() {
    clearTimeout(this.refetch);
    this.refetch = setTimeout(() => void this.load(), 800);
  }

  private async load() {
    try {
      const list = await this.api.inbox({ orphans: this.orphans() });
      this.items.set(list);
      this.state.update(s => Object.fromEntries(Object.entries(s).filter(([id, v]) => v.decided || list.some(l => l.id === id))));
    } catch (e) {
      this.toasts.problem(e as Problem);
    } finally {
      this.loaded.set(true);
    }
  }
}
