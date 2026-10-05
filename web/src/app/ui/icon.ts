import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';

/**
 * Authored pictograms, station-signage grammar: 24-unit grid, 2px stroke, square caps, no fills
 * except where a sign would be solid. Decorative by default (aria-hidden); label the control, not the icon.
 */
const PATHS: Record<string, string> = {
  clock: 'M12 3a9 9 0 1 0 0 18a9 9 0 0 0 0-18M12 7v5l3.5 2',
  check: 'M4.5 12.5l5 5L19.5 7',
  cross: 'M6 6l12 12M18 6L6 18',
  back: 'M9 6L4 11l5 5M4.5 11H14a5.5 5.5 0 0 1 0 11h-2',
  person: 'M12 4a3.5 3.5 0 1 0 0 7a3.5 3.5 0 0 0 0-7M5 20c.8-3.6 3.6-6 7-6s6.2 2.4 7 6',
  people: 'M9 5a3 3 0 1 0 0 6a3 3 0 0 0 0-6M3 19c.7-3 3-5 6-5s5.3 2 6 5M16 6.5a2.6 2.6 0 0 1 0 5M17.5 14.5c1.8.6 3 2.2 3.5 4.5',
  inbox: 'M3 13l3-8h12l3 8v6H3zM3 13h5l1.5 2.5h5L16 13h5',
  calendar: 'M4 6h16v14H4zM4 10h16M8 3v4M16 3v4',
  key: 'M8 15a4 4 0 1 1 3.5-6M11 11h10M17 11v3M20 11v4',
  hand: 'M8 13V6a1.5 1.5 0 0 1 3 0v5M11 11V4.5a1.5 1.5 0 0 1 3 0V11M14 11V6a1.5 1.5 0 0 1 3 0v7c0 4-2.5 7-6 7c-2.6 0-4-1.4-5.5-3.5L3.8 13.6a1.5 1.5 0 0 1 2.4-1.8L8 14',
  plus: 'M12 5v14M5 12h14',
  pencil: 'M4 20h4L19 9l-4-4L4 16zM13.5 6.5l4 4',
  exit: 'M14 4H5v16h9M10 12h11M17 8l4 4l-4 4',
  globe: 'M12 3a9 9 0 1 0 0 18a9 9 0 0 0 0-18M3 12h18M12 3c2.5 2.5 3.5 5.5 3.5 9s-1 6.5-3.5 9c-2.5-2.5-3.5-5.5-3.5-9s1-6.5 3.5-9',
  signal: 'M5 19v-3M9.67 19v-6M14.33 19v-9M19 19V7',
  lock: 'M6 11h12v9H6zM8.5 11V8a3.5 3.5 0 0 1 7 0v3',
  manual: 'M6 3h9l3 3v15H6zM9 11h6M9 15h6M9 7h3',
  punch: 'M7 3h10v18H7zM10 7h4M10 11h4M12 15v3',
  link: 'M10 14l4-4M8.5 11.5L6.5 13.5a3.5 3.5 0 0 0 5 5l2-2M15.5 12.5l2-2a3.5 3.5 0 0 0-5-5l-2 2',
  unlink: 'M8.5 11.5L6.5 13.5a3.5 3.5 0 0 0 5 5l2-2M15.5 12.5l2-2a3.5 3.5 0 0 0-5-5l-2 2M4 4l16 16',
  arrow: 'M5 12h14M13 6l6 6l-6 6',
  chevronL: 'M15 5l-7 7l7 7',
  chevronR: 'M9 5l7 7l-7 7',
  power: 'M12 3v8M7 6.5a7 7 0 1 0 10 0',
};

@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { 'aria-hidden': 'true', class: 'inline-block shrink-0' },
  template: `<svg [attr.width]="size()" [attr.height]="size()" viewBox="0 0 24 24" fill="none" stroke="currentColor"
    stroke-width="2" stroke-linecap="square" stroke-linejoin="miter" class="block"><path [attr.d]="d()" /></svg>`,
})
export class Icon {
  name = input.required<string>();
  size = input(20);
  protected d = computed(() => PATHS[this.name()] ?? '');
}
