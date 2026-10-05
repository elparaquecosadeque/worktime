import { DestroyRef, Injectable, effect, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, HttpTransportType, LogLevel } from '@microsoft/signalr';
import { Subject, filter } from 'rxjs';
import { Auth } from './auth';

export type RealtimeName =
  | 'PresenceChanged' | 'PunchChanged' | 'WorkLogChanged' | 'WorkLogStatusChanged'
  | 'AssignmentChanged' | 'AssignmentRequested' | 'AssignmentResolved' | 'ForceLogout' | 'DemoReset';

export interface RealtimeEvent { name: RealtimeName; payload: any; }

export type ConnectionState = 'connecting' | 'live' | 'reconnecting' | 'offline';

const EVENTS: RealtimeName[] = [
  'PresenceChanged', 'PunchChanged', 'WorkLogChanged', 'WorkLogStatusChanged',
  'AssignmentChanged', 'AssignmentRequested', 'AssignmentResolved', 'ForceLogout', 'DemoReset',
];

/**
 * One hub connection per signed-in session. WebSockets only with skipNegotiation, so it works behind a
 * round-robin balancer without sticky sessions (the Redis backplane fans events out across replicas).
 * Every reconnect emits `resync`: screens refetch their full state, since events missed while away are gone.
 */
@Injectable({ providedIn: 'root' })
export class Realtime {
  private auth = inject(Auth);
  private hub: HubConnection | null = null;
  private heartbeat: ReturnType<typeof setInterval> | undefined;
  private events = new Subject<RealtimeEvent>();

  readonly state = signal<ConnectionState>('offline');
  readonly resync = new Subject<void>();

  constructor() {
    effect(() => {
      const token = this.auth.token();
      void this.stop();
      if (token) void this.start();
    });
  }

  /** Subscribes for the lifetime of the calling component. */
  on(names: RealtimeName[], handler: (e: RealtimeEvent) => void, destroyRef = inject(DestroyRef)) {
    const sub = this.events.pipe(filter(e => names.includes(e.name))).subscribe(handler);
    destroyRef.onDestroy(() => sub.unsubscribe());
  }

  onResync(handler: () => void, destroyRef = inject(DestroyRef)) {
    const sub = this.resync.subscribe(handler);
    destroyRef.onDestroy(() => sub.unsubscribe());
  }

  private async start() {
    const hub = new HubConnectionBuilder()
      .withUrl('/hubs/worktime', {
        accessTokenFactory: () => this.auth.token() ?? '',
        transport: HttpTransportType.WebSockets,
        skipNegotiation: true,
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000, 20000, 30000])
      .configureLogging(LogLevel.Warning)
      .build();
    this.hub = hub;

    for (const name of EVENTS) hub.on(name, (payload: unknown) => this.events.next({ name, payload }));
    hub.on('ForceLogout', (p: { reason: string }) => this.auth.signOut(p?.reason ?? 'auth.stale_session'));
    hub.on('DemoReset', () => this.auth.signOut('demo.reset'));

    hub.onreconnecting(() => this.state.set('reconnecting'));
    hub.onreconnected(() => { this.state.set('live'); this.resync.next(); });
    hub.onclose(() => { if (this.hub === hub) this.state.set('offline'); });

    this.state.set('connecting');
    try {
      await hub.start();
      if (this.hub !== hub) return;
      this.state.set('live');
      this.heartbeat = setInterval(() => {
        if (hub.state === HubConnectionState.Connected) hub.invoke('Heartbeat').catch(() => undefined);
      }, 30_000);
    } catch {
      if (this.hub === hub) this.state.set('offline');
    }
  }

  private async stop() {
    clearInterval(this.heartbeat);
    const hub = this.hub;
    this.hub = null;
    this.state.set('offline');
    if (hub) await hub.stop().catch(() => undefined);
  }
}
