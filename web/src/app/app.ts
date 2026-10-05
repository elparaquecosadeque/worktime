import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { Toasts } from './core/feedback';
import { Icon } from './ui/icon';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, Icon],
  template: `
    <router-outlet />
    <div class="pointer-events-none fixed inset-x-0 bottom-20 z-50 flex flex-col items-center gap-2 px-4 sm:bottom-6" aria-live="polite">
      @for (t of toasts.items(); track t.id) {
        <div class="toast pointer-events-auto flex w-full max-w-md items-start gap-3 rounded-[2px] px-4 pb-2.5 pt-3 shadow-lift"
          [class]="t.tone === 'error' ? 'bg-stop text-white' : t.tone === 'ok' ? 'bg-go text-white' : 'bg-ink text-ground'">
          <app-icon [name]="t.tone === 'error' ? 'cross' : t.tone === 'ok' ? 'check' : 'signal'" [size]="18" class="mt-px" />
          <p class="flex-1 text-sm font-semibold leading-snug">{{ t.text }}</p>
          <button type="button" class="-mr-1 opacity-80 hover:opacity-100" (click)="toasts.dismiss(t.id)" i18n-aria-label="@@toast.close" aria-label="Cerrar aviso">
            <app-icon name="cross" [size]="16" />
          </button>
        </div>
      }
    </div>`,
  styles: `.toast { animation: toast-in 220ms var(--ease-out-expo); } @keyframes toast-in { from { transform: translateY(8px); opacity: 0; } }`,
})
export class App {
  protected toasts = inject(Toasts);
}
