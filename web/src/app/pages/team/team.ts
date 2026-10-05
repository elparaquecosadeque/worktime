import { ChangeDetectionStrategy, Component, LOCALE_ID, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Api } from '../../core/api';
import { Auth } from '../../core/auth';
import { Toasts } from '../../core/feedback';
import { Perms, PresenceState, Problem, TeamGroupDto, TeamMemberDto, TeamSummaryDto } from '../../core/models';
import { Realtime } from '../../core/realtime';
import { Clock, duration } from '../../core/time';
import { Icon } from '../../ui/icon';
import { PresencePlate } from '../../ui/plates';

/**
 * Live team board. Cells never move (alphabetical, fixed furniture per person); only their signs change.
 * Presence and punches are patched from events; aggregates come from the 5 s cached summary.
 */
@Component({
  selector: 'app-team',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon, PresencePlate, RouterLink],
  template: `
    <div class="flex flex-wrap items-end justify-between gap-4">
      <h1 class="text-2xl font-extrabold sm:text-3xl">
        @if (all()) { <span i18n="@@team.titleAll">Todos los equipos</span> } @else { <span i18n="@@team.title">Tu equipo</span> }
      </h1>
      <p class="text-sm font-semibold text-ink-2" i18n="@@team.counts">{{ counts().total }} personas · {{ counts().working }} trabajando · {{ counts().online }} en línea</p>
    </div>

    @if (summary(); as s) {
      @for (g of s.groups; track g.supervisorId ?? 'orphans') {
        @if (all()) {
          <h2 class="mt-10 flex items-center gap-3 border-b border-ink pb-2 text-lg font-extrabold">
            @if (g.supervisorId) {
              <app-icon name="person" />{{ g.supervisorName }}
            } @else {
              <span class="plate bg-wait text-ink" i18n="@@team.orphans">Sin supervisor</span>
              @if (g.members.length) { <a routerLink="/bandeja" [queryParams]="{ huerfanos: 1 }" class="ml-auto text-sm font-bold" i18n="@@team.orphansInbox">Ver sus pendientes</a> }
            }
          </h2>
        }
        <ul class="mt-5 grid border-l border-t border-rule sm:grid-cols-2 lg:grid-cols-3">
          @for (m of g.members; track m.workerId) {
            <li class="flex flex-col gap-3 border-b border-r border-rule bg-card p-4">
              <div class="flex items-start justify-between gap-3">
                <span class="text-lg font-bold leading-tight">{{ m.name }}</span>
                @if (m.pendingCount) {
                  <a routerLink="/bandeja" [queryParams]="{ trabajador: m.workerId }" class="plate shrink-0 bg-wait text-ink no-underline"
                    i18n="@@team.pendingCount">{{ m.pendingCount }} por decidir</a>
                }
              </div>
              <app-presence [state]="m.presence" [since]="m.workingSince ? clockFor(m).time(m.workingSince) : ''" />
              <dl class="grid grid-cols-2 gap-x-4 text-sm">
                <dt class="text-ink-2" i18n="@@team.approvedMonth">Aprobadas este mes</dt>
                <dt class="text-ink-2" i18n="@@team.pendingMonth">Pendientes</dt>
                <dd class="text-lg font-extrabold tabular-nums">{{ fmt(m.monthHoursApproved) }}</dd>
                <dd class="text-lg font-extrabold tabular-nums">{{ fmt(m.monthHoursPending) }}</dd>
              </dl>
              @if (m.needsRevisionCount) {
                <p class="text-sm text-ink-2" i18n="@@team.revisionCount">{{ m.needsRevisionCount }} esperando su corrección</p>
              }
            </li>
          } @empty {
            <li class="border-b border-r border-rule bg-card p-5 text-ink-2 sm:col-span-2 lg:col-span-3">
              @if (g.supervisorId === null) { <span i18n="@@team.noOrphans">Todos los trabajadores tienen supervisor.</span> }
              @else { <span i18n="@@team.empty">Todavía no hay trabajadores aquí. Créalos o pide al admin que te asigne.</span> }
            </li>
          }
        </ul>
      }
    } @else {
      <div class="mt-5 grid gap-px sm:grid-cols-2 lg:grid-cols-3">
        @for (i of [1, 2, 3, 4, 5, 6]; track i) { <div class="skeleton h-40"></div> }
      </div>
    }`,
})
export class TeamPage {
  private api = inject(Api);
  private auth = inject(Auth);
  private toasts = inject(Toasts);
  private locale = inject(LOCALE_ID);
  private clocks = new Map<string, Clock>();
  private refetch: ReturnType<typeof setTimeout> | undefined;

  protected summary = signal<TeamSummaryDto | null>(null);
  protected all = computed(() => this.auth.has(Perms.MonitorAll));
  protected fmt = duration;

  protected counts = computed(() => {
    const members = this.summary()?.groups.flatMap(g => g.members) ?? [];
    return {
      total: members.length,
      working: members.filter(m => m.presence === 'Working').length,
      online: members.filter(m => m.presence === 'Online').length,
    };
  });

  constructor() {
    const realtime = inject(Realtime);
    realtime.on(['PresenceChanged'], e => this.patch(e.payload.workerId, m =>
      m.presence === 'Working' ? m : { ...m, presence: (e.payload.online ? 'Online' : 'Offline') as PresenceState }));
    realtime.on(['PunchChanged'], e => this.patch(e.payload.workerId, m => ({
      ...m, workingSince: e.payload.workingSince,
      presence: e.payload.workingSince ? 'Working' : 'Online',
    })));
    realtime.on(['WorkLogChanged', 'WorkLogStatusChanged', 'AssignmentChanged', 'PunchChanged'], () => this.scheduleRefetch());
    realtime.onResync(() => void this.load());
    void this.load();
  }

  protected clockFor(m: TeamMemberDto) {
    let c = this.clocks.get(m.timeZoneId);
    if (!c) this.clocks.set(m.timeZoneId, (c = new Clock(this.locale, m.timeZoneId)));
    return c;
  }

  private patch(workerId: string, fn: (m: TeamMemberDto) => TeamMemberDto) {
    this.summary.update(s => s && {
      ...s,
      groups: s.groups.map((g: TeamGroupDto) => ({ ...g, members: g.members.map(m => (m.workerId === workerId ? fn(m) : m)) })),
    });
  }

  /** Aggregates (hours, counts) refresh once after a burst of events. */
  private scheduleRefetch() {
    clearTimeout(this.refetch);
    // Just past the 5 s server cache, so the refetch never resurrects a state the events already replaced.
    this.refetch = setTimeout(() => void this.load(), 5500);
  }

  private async load() {
    try {
      this.summary.set(await this.api.teamSummary());
    } catch (e) {
      this.toasts.problem(e as Problem);
    }
  }
}
