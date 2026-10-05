import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { PresenceState, WorkLogStatus } from '../core/models';
import { statusLabel } from '../core/time';
import { Icon } from './icon';

/** Status as an enamel sign: colour + pictogram + word. Never colour alone. */
export const STATUS_STYLE: Record<WorkLogStatus, { cls: string; icon: string }> = {
  Pending: { cls: 'bg-wait text-ink', icon: 'clock' },
  NeedsRevision: { cls: 'bg-back text-white', icon: 'back' },
  Approved: { cls: 'bg-go text-white', icon: 'check' },
  Rejected: { cls: 'bg-stop text-white', icon: 'cross' },
};

@Component({
  selector: 'app-status',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [Icon],
  template: `<span class="plate" [class]="style().cls"><app-icon [name]="style().icon" [size]="13" />{{ label() }}</span>`,
})
export class StatusPlate {
  status = input.required<WorkLogStatus>();
  protected style = computed(() => STATUS_STYLE[this.status()]);
  protected label = computed(() => statusLabel(this.status()));
}

@Component({
  selector: 'app-presence',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @switch (state()) {
      @case ('Working') {
        <span class="plate bg-go text-white"><span class="size-2 rounded-full bg-white motion-safe:animate-pulse"></span>
          <ng-container i18n="@@presence.workingSince">Trabajando desde {{ since() }}</ng-container></span>
      }
      @case ('Online') {
        <span class="plate bg-sign text-sign-ink"><span class="size-2 rounded-full bg-white"></span><ng-container i18n="@@presence.online">En línea</ng-container></span>
      }
      @default {
        <span class="plate text-ink-2 shadow-[inset_0_0_0_1px_var(--color-rule)]"><span class="size-2 rounded-full border border-idle"></span><ng-container i18n="@@presence.offline">Desconectado</ng-container></span>
      }
    }`,
})
export class PresencePlate {
  state = input.required<PresenceState>();
  since = input<string>('');
}
