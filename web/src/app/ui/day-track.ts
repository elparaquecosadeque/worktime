import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { WorkLogStatus } from '../core/models';
import { Clock } from '../core/time';

export interface TrackSegment { start: string; end: string | null; status: WorkLogStatus | 'Open'; }

const FILL: Record<TrackSegment['status'], string> = {
  Pending: 'var(--color-wait)', NeedsRevision: 'var(--color-back)', Approved: 'var(--color-go)', Rejected: 'var(--color-stop)', Open: 'var(--color-go)',
};

/**
 * The day as a platform track: 00–24 h, every segment exactly as long as the time it covers.
 * An open punch grows to "now" with a hatched fill.
 */
@Component({
  selector: 'app-day-track',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="relative h-10 rounded-[2px] bg-panel" role="img" [attr.aria-label]="label()">
      @for (h of hours; track h) {
        <span class="absolute inset-y-0 w-px bg-rule" [style.left.%]="h / 24 * 100"></span>
      }
      @for (s of bars(); track $index) {
        <span class="absolute inset-y-1.5 rounded-[2px]" [style.left.%]="s.left" [style.width.%]="s.width"
          [style.background]="s.fill" [class.track-open]="s.open"></span>
      }
      <span class="absolute -inset-y-1 w-0.5 bg-stop" [style.left.%]="nowAt()"></span>
    </div>
    <div class="relative mt-1 h-4 text-[0.6875rem] font-semibold text-ink-3">
      @for (h of labels; track h) {
        <span class="absolute -translate-x-1/2" [style.left.%]="h / 24 * 100">{{ h.toString().padStart(2, '0') }}</span>
      }
    </div>`,
  styles: `.track-open { background-image: repeating-linear-gradient(135deg, transparent 0 5px, rgb(255 255 255 / .35) 5px 9px) !important; }`,
})
export class DayTrack {
  clock = input.required<Clock>();
  segments = input.required<TrackSegment[]>();
  now = input.required<Date>();
  label = input('');

  protected readonly hours = [3, 6, 9, 12, 15, 18, 21];
  protected readonly labels = [0, 6, 12, 18, 24];

  private pos(iso: string | Date) {
    const p = this.clock().parts(iso);
    return ((p.hour * 60 + p.minute) / 1440) * 100;
  }

  protected nowAt = computed(() => this.pos(this.now()));

  protected bars = computed(() => this.segments().map(s => {
    const left = this.pos(s.start);
    const right = s.end ? this.pos(s.end) || 100 : this.nowAt();
    return { left, width: Math.max(right - left, 0.4), fill: FILL[s.status], open: s.status === 'Open' };
  }));
}
