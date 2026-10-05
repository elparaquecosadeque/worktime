import { ChangeDetectionStrategy, Component, ElementRef, afterNextRender, inject, input, viewChild, DestroyRef } from '@angular/core';

/**
 * A station clock. Its one authored motion: the red second hand sweeps the dial in 58.5 s and rests at
 * twelve for a beat until the minute hand jumps — the railway way of keeping every clock in step.
 * Driven by requestAnimationFrame writing transforms directly (no change detection per frame).
 */
@Component({
  selector: 'app-station-clock',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { role: 'img', '[attr.aria-label]': 'label()' },
  template: `
    <svg viewBox="0 0 200 200" class="block size-full">
      <circle cx="100" cy="100" r="97" fill="var(--color-card)" stroke="var(--color-ink)" stroke-width="4" />
      @for (i of ticks; track i) {
        <rect [attr.x]="i % 5 === 0 ? 97 : 98.75" y="9" [attr.width]="i % 5 === 0 ? 6 : 2.5" [attr.height]="i % 5 === 0 ? 22 : 7"
          fill="var(--color-ink)" [attr.transform]="'rotate(' + i * 6 + ' 100 100)'" />
      }
      <g #hour><rect x="95" y="46" width="10" height="66" fill="var(--color-ink)" /></g>
      <g #minute><rect x="96.5" y="17" width="7" height="95" fill="var(--color-ink)" /></g>
      <g #second>
        <rect x="98.6" y="24" width="2.8" height="104" fill="var(--color-stop)" />
        <rect x="94" y="22" width="12" height="12" fill="var(--color-stop)" />
      </g>
      <circle cx="100" cy="100" r="4" fill="var(--color-ink)" />
    </svg>`,
})
export class StationClock {
  /** Zone whose wall clock is shown (the worker's). */
  zone = input.required<string>();
  label = input('');

  protected readonly ticks = Array.from({ length: 60 }, (_, i) => i);
  private hour = viewChild.required<ElementRef<SVGGElement>>('hour');
  private minute = viewChild.required<ElementRef<SVGGElement>>('minute');
  private second = viewChild.required<ElementRef<SVGGElement>>('second');

  constructor() {
    const destroyRef = inject(DestroyRef);
    afterNextRender(() => {
      const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
      const fmt = new Intl.DateTimeFormat('en-US', { timeZone: this.zone(), hour: 'numeric', minute: 'numeric', second: 'numeric', hourCycle: 'h23' });
      let raf = 0;
      const frame = () => {
        const now = new Date();
        const [h, m, s] = fmt.format(now).split(':').map(Number);
        const fraction = s + now.getMilliseconds() / 1000;
        const secAngle = reduce ? s * 6 : Math.min(fraction / 58.5, 1) * 360;
        this.second().nativeElement.setAttribute('transform', `rotate(${secAngle} 100 100)`);
        this.minute().nativeElement.setAttribute('transform', `rotate(${m * 6} 100 100)`); // jumps once a minute
        this.hour().nativeElement.setAttribute('transform', `rotate(${(h % 12) * 30 + m * 0.5} 100 100)`);
        raf = reduce ? window.setTimeout(frame, 1000 - now.getMilliseconds()) : requestAnimationFrame(frame);
      };
      frame();
      destroyRef.onDestroy(() => (reduce ? clearTimeout(raf) : cancelAnimationFrame(raf)));
    });
  }
}
