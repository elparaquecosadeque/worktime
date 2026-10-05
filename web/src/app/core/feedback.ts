import { Injectable, signal } from '@angular/core';
import { Problem } from './models';

/** Human copy for every stable API code. The API never sends prose; the client owns the language. */
export function messageFor(code: string): string {
  switch (code) {
    case 'auth.invalid_credentials': return $localize`:@@err.auth.invalid:Email o contraseña incorrectos.`;
    case 'auth.stale_session': return $localize`:@@err.auth.stale:Tu sesión ya no es válida. Vuelve a entrar.`;
    case 'auth.required': return $localize`:@@err.auth.required:Tu sesión terminó. Vuelve a entrar.`;
    case 'auth.forbidden': return $localize`:@@err.auth.forbidden:Tu rol no tiene permiso para esta acción.`;
    case 'demo.reset': return $localize`:@@err.demo.reset:La demo se reinició con datos nuevos. Vuelve a entrar.`;
    case 'permissions.changed': return $localize`:@@err.perms.changed:Tus permisos cambiaron. Vuelve a entrar para usarlos.`;
    case 'user.deactivated': return $localize`:@@err.user.deactivated:Tu cuenta fue desactivada.`;
    case 'rate.limited': return $localize`:@@err.rate:Demasiados intentos. Espera un minuto.`;
    case 'network.offline': return $localize`:@@err.network:No hay conexión con el servidor.`;
    case 'reason.required': return $localize`:@@err.reason:Escribe una razón.`;
    case 'worklog.already_decided': return $localize`:@@err.wl.decided:Otra persona ya decidió este registro.`;
    case 'concurrency.stale': return $localize`:@@err.stale:Alguien cambió este registro al mismo tiempo. Lo actualizamos.`;
    case 'worklog.not_pending': return $localize`:@@err.wl.notPending:El registro está esperando la corrección del trabajador.`;
    case 'worklog.not_editable': return $localize`:@@err.wl.notEditable:Los registros aprobados o rechazados no se editan.`;
    case 'worklog.not_your_worker': return $localize`:@@err.wl.notYours:Este trabajador ya no está en tu equipo.`;
    case 'worklog.self_decision': return $localize`:@@err.wl.self:Nadie decide sobre sus propios registros.`;
    case 'worklog.overlap': return $localize`:@@err.wl.overlap:Se cruza con otro registro tuyo.`;
    case 'worklog.invalid_range': return $localize`:@@err.wl.range:La salida debe ser posterior a la entrada.`;
    case 'worklog.too_long': return $localize`:@@err.wl.long:Un registro no puede superar 16 horas.`;
    case 'worklog.in_future': return $localize`:@@err.wl.future:No se registran horas futuras.`;
    case 'worklog.too_old': return $localize`:@@err.wl.old:Solo se registran horas de los últimos 31 días.`;
    case 'worklog.crosses_midnight': return $localize`:@@err.wl.midnight:Un registro no puede pasar la medianoche. Divídelo en dos.`;
    case 'punch.already_open': return $localize`:@@err.punch.open:Ya estás marcando: tu entrada se registró antes.`;
    case 'punch.not_open': return $localize`:@@err.punch.notOpen:No tienes una entrada abierta.`;
    case 'assignment.already_pending': return $localize`:@@err.as.pending:Ya tienes una solicitud pendiente.`;
    case 'assignment.already_assigned': return $localize`:@@err.as.assigned:Ya tienes supervisor.`;
    case 'assignment.already_resolved': return $localize`:@@err.as.resolved:Otro admin ya resolvió esta solicitud.`;
    case 'assignment.not_assigned': return $localize`:@@err.as.notAssigned:Este trabajador no tiene supervisor.`;
    case 'assignment.invalid_supervisor': return $localize`:@@err.as.invalidSup:Elige un supervisor activo.`;
    case 'user.email_taken': return $localize`:@@err.user.email:Ya existe una cuenta con ese email.`;
    case 'user.email_invalid': return $localize`:@@err.user.emailInvalid:El email no es válido.`;
    case 'user.name_required': return $localize`:@@err.user.name:Escribe el nombre.`;
    case 'user.password_too_short': return $localize`:@@err.user.password:La contraseña necesita al menos 8 caracteres.`;
    case 'user.timezone_invalid': return $localize`:@@err.user.tz:Zona horaria desconocida.`;
    case 'user.not_managed': return $localize`:@@err.user.notManaged:Esta persona no está en tu equipo.`;
    case 'user.self_deactivation': return $localize`:@@err.user.self:No puedes desactivar tu propia cuenta.`;
    case 'permission.locked': return $localize`:@@err.perm.locked:Ese permiso del admin está bloqueado.`;
    default: return $localize`:@@err.generic:Algo falló. Inténtalo de nuevo.`;
  }
}

export interface Toast { id: number; text: string; tone: 'info' | 'ok' | 'error'; }

@Injectable({ providedIn: 'root' })
export class Toasts {
  private seq = 0;
  readonly items = signal<Toast[]>([]);

  show(text: string, tone: Toast['tone'] = 'info') {
    const id = ++this.seq;
    this.items.update(list => [...list.slice(-2), { id, text, tone }]);
    setTimeout(() => this.dismiss(id), tone === 'error' ? 7000 : 4000);
  }

  problem(p: Problem) { this.show(messageFor(p.code), 'error'); }

  dismiss(id: number) { this.items.update(list => list.filter(t => t.id !== id)); }
}
