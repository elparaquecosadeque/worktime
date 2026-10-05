import { ChangeDetectionStrategy, Component, LOCALE_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Api } from '../../core/api';
import { Toasts, messageFor } from '../../core/feedback';
import { AssignmentRequestDto, Problem, SupervisorRow } from '../../core/models';
import { Realtime } from '../../core/realtime';
import { Clock, browserZone } from '../../core/time';
import { Icon } from '../../ui/icon';

interface Draft { supervisorId: string | null; dismissing: boolean; reason: string; error?: string; }

@Component({
  selector: 'app-requests',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, Icon],
  template: `
    <h1 class="text-2xl font-extrabold sm:text-3xl" i18n="@@requests.title">Solicitudes de supervisor</h1>
    <p class="mt-1 text-sm font-semibold text-ink-2" i18n="@@requests.count">{{ items().length }} esperando</p>

    <ul class="mt-5 divide-y divide-rule border-y border-rule">
      @for (r of items(); track r.id) {
        @let d = drafts()[r.id];
        <li class="grid gap-x-6 gap-y-3 py-5 md:grid-cols-[minmax(0,1fr)_auto] md:items-center">
          <div class="min-w-0">
            <p class="font-bold">{{ r.workerName }}</p>
            <p class="text-sm text-ink-2" i18n="@@requests.sent">Enviada {{ clock.stamp(r.createdAt) }}</p>
            @if (r.note) { <p class="mt-2 max-w-prose">“{{ r.note }}”</p> }
            @if (r.preferredSupervisorName) { <p class="mt-1 text-sm" i18n="@@requests.suggested">Sugiere a <span class="font-bold">{{ r.preferredSupervisorName }}</span></p> }
          </div>
          @if (d?.dismissing) {
            <form class="flex flex-wrap items-center gap-2" (ngSubmit)="dismiss(r)">
              <input class="input !min-h-9 w-60" name="reason" required [ngModel]="d.reason" (ngModelChange)="patch(r.id, { reason: $event })"
                i18n-placeholder="@@requests.dismissPh" placeholder="Por qué se descarta" i18n-aria-label="@@inbox.reasonLabel" aria-label="Razón" />
              <button type="submit" class="btn btn-stop btn-sm" [disabled]="!d.reason.trim()" i18n="@@requests.dismiss">Descartar</button>
              <button type="button" class="btn btn-quiet btn-sm" (click)="patch(r.id, { dismissing: false })" i18n="@@common.cancel">Cancelar</button>
            </form>
          } @else {
            <form class="flex flex-wrap items-center gap-2" (ngSubmit)="fulfill(r)">
              <select class="input !min-h-9 !w-auto" name="sup" [ngModel]="d?.supervisorId ?? r.preferredSupervisorId" (ngModelChange)="patch(r.id, { supervisorId: $event })"
                i18n-aria-label="@@people.supervisorPick" aria-label="Supervisor">
                <option [ngValue]="null" i18n="@@people.pickSupervisor">Elige supervisor…</option>
                @for (s of supervisors(); track s.id) { <option [ngValue]="s.id">{{ s.name }}</option> }
              </select>
              <button type="submit" class="btn btn-sm" [disabled]="!(d?.supervisorId ?? r.preferredSupervisorId)"><app-icon name="link" [size]="15" /><ng-container i18n="@@people.assign">Asignar</ng-container></button>
              <button type="button" class="btn btn-quiet btn-sm" (click)="patch(r.id, { dismissing: true })" i18n="@@requests.dismiss">Descartar</button>
            </form>
          }
          @if (d?.error) { <p class="text-sm font-semibold text-stop md:col-span-2" role="alert">{{ d!.error }}</p> }
        </li>
      } @empty {
        <li class="py-10">
          <p class="text-lg font-bold" i18n="@@requests.emptyTitle">No hay solicitudes.</p>
          <p class="mt-1 text-ink-2" i18n="@@requests.emptyBody">Cuando un trabajador sin supervisor pida uno, aparecerá aquí en vivo.</p>
        </li>
      }
    </ul>`,
})
export class RequestsPage {
  private api = inject(Api);
  private toasts = inject(Toasts);
  protected clock = new Clock(inject(LOCALE_ID), browserZone());
  protected items = signal<AssignmentRequestDto[]>([]);
  protected supervisors = signal<SupervisorRow[]>([]);
  protected drafts = signal<Record<string, Draft>>({});

  constructor() {
    const realtime = inject(Realtime);
    realtime.on(['AssignmentRequested', 'AssignmentResolved', 'AssignmentChanged'], () => void this.load());
    realtime.onResync(() => void this.load());
    void this.load();
    this.api.supervisors().then(s => this.supervisors.set(s), () => undefined);
  }

  protected patch(id: string, p: Partial<Draft>) {
    const empty: Draft = { supervisorId: null, dismissing: false, reason: '' };
    this.drafts.update(d => ({ ...d, [id]: { ...empty, ...d[id], ...p } }));
  }

  private async load() {
    try { this.items.set(await this.api.pendingRequests()); } catch (e) { this.toasts.problem(e as Problem); }
  }

  protected async fulfill(r: AssignmentRequestDto) {
    const supervisorId = this.drafts()[r.id]?.supervisorId ?? r.preferredSupervisorId;
    if (!supervisorId) return;
    try {
      await this.api.fulfill(r.id, supervisorId);
      this.toasts.show($localize`:@@requests.assigned:${r.workerName}:name: ya tiene supervisor.`, 'ok');
      await this.load();
    } catch (e) {
      this.patch(r.id, { error: messageFor((e as Problem).code) });
      await this.load();
    }
  }

  protected async dismiss(r: AssignmentRequestDto) {
    try {
      await this.api.dismiss(r.id, this.drafts()[r.id].reason.trim());
      await this.load();
    } catch (e) {
      this.patch(r.id, { error: messageFor((e as Problem).code) });
    }
  }
}
