import { Injectable, inject, signal } from '@angular/core';
import { CanMatchFn } from '@angular/router';
import { Api } from './api';
import { CurrentTenant } from './models';

/**
 * One build serves every host: the API says which tenant this host is (from the Host header). No tenant means the
 * root host (or an unknown slug), where the only screen is the space picker.
 */
@Injectable({ providedIn: 'root' })
export class Tenancy {
  private api = inject(Api);
  readonly current = signal<CurrentTenant | null>(null);

  async load() {
    try {
      this.current.set(await this.api.currentTenant());
    } catch {
      this.current.set(null);
    }
  }
}

/** {@link document.baseURI} on another host: keeps the scheme, the port and the locale prefix (/es/, /en/). */
export function urlOnHost(host: string, path: string): string {
  const url = new URL(document.baseURI);
  url.hostname = host;
  return new URL(path, url).href;
}

export const onRootHost: CanMatchFn = () => inject(Tenancy).current() === null;
