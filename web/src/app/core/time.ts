import { LOCALE_ID, inject } from '@angular/core';
import { WorkLogStatus } from './models';

/** Formatting in an explicit IANA zone: a worker's hours always read in the worker's own clock. */
export class Clock {
  constructor(private locale: string, readonly zone: string) {}

  time = (iso: string | Date) => new Intl.DateTimeFormat(this.locale, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone: this.zone }).format(new Date(iso));
  day = (iso: string | Date) => new Intl.DateTimeFormat(this.locale, { weekday: 'short', day: 'numeric', month: 'short', timeZone: this.zone }).format(new Date(iso));
  dayLong = (iso: string | Date) => new Intl.DateTimeFormat(this.locale, { weekday: 'long', day: 'numeric', month: 'long', timeZone: this.zone }).format(new Date(iso));
  monthTitle = (year: number, month: number) => new Intl.DateTimeFormat(this.locale, { month: 'long', year: 'numeric', timeZone: 'UTC' }).format(Date.UTC(year, month - 1, 15));
  stamp = (iso: string | Date) => new Intl.DateTimeFormat(this.locale, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone: this.zone }).format(new Date(iso));

  /** Wall-clock parts of an instant in this zone. */
  parts(iso: string | Date) {
    const p = Object.fromEntries(new Intl.DateTimeFormat('en-US', {
      timeZone: this.zone, year: 'numeric', month: 'numeric', day: 'numeric', hour: 'numeric', minute: 'numeric', second: 'numeric', hourCycle: 'h23',
    }).formatToParts(new Date(iso)).map(x => [x.type, Number(x.value)]));
    return { year: p['year'], month: p['month'], day: p['day'], hour: p['hour'], minute: p['minute'], second: p['second'] };
  }

  /** Local date key yyyy-mm-dd in this zone. */
  dateKey = (iso: string | Date) => { const p = this.parts(iso); return `${p.year}-${pad(p.month)}-${pad(p.day)}`; };

  /** Converts a wall-clock "yyyy-mm-ddThh:mm" in this zone to a UTC ISO instant. */
  toInstant(local: string): string {
    const guess = new Date(local + 'Z');
    const p = this.parts(guess);
    const asIfUtc = Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute);
    return new Date(guess.getTime() - (asIfUtc - guess.getTime())).toISOString();
  }

  /** Inverse of {@link toInstant}, for <input type="datetime-local"> values. */
  toLocalInput(iso: string): string {
    const p = this.parts(iso);
    return `${p.year}-${pad(p.month)}-${pad(p.day)}T${pad(p.hour)}:${pad(p.minute)}`;
  }
}

export function injectClock(zone: string) { return new Clock(inject(LOCALE_ID), zone); }

export const pad = (n: number) => String(n).padStart(2, '0');

export function hours(startIso: string, endIso: string) { return (new Date(endIso).getTime() - new Date(startIso).getTime()) / 3_600_000; }

/** "7 h 45 min" — tabular, never decimals. */
export function duration(h: number) {
  const total = Math.max(0, Math.round(h * 60));
  const hh = Math.floor(total / 60), mm = total % 60;
  return hh === 0 ? `${mm} min` : mm === 0 ? `${hh} h` : `${hh} h ${pad(mm)} min`;
}

export function statusLabel(s: WorkLogStatus): string {
  switch (s) {
    case 'Pending': return $localize`:@@status.pending:Pendiente`;
    case 'NeedsRevision': return $localize`:@@status.revision:En revisión`;
    case 'Approved': return $localize`:@@status.approved:Aprobado`;
    case 'Rejected': return $localize`:@@status.rejected:Rechazado`;
  }
}

export const browserZone = () => Intl.DateTimeFormat().resolvedOptions().timeZone;
