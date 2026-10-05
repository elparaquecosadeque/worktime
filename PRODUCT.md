# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Stack

Angular 22 (standalone, signals, zoneless) + Angular CDK + Tailwind v4 + i18n nativa (`@angular/localize`, es por defecto, en). Backend ASP.NET Core 8 + SignalR + EF Core/PostgreSQL + Redis, servido detrás de nginx con docker compose. Ver `docs/spec.md`.

## Users

- **Trabajador:** marca punch-in/out y revisa sus horas del mes, desde el móvil o desde el desktop por igual. Necesita saber de un vistazo si está marcando, cuánto lleva y en qué estado está cada registro (y por qué).
- **Supervisor:** revisa y decide sobre los registros de los trabajadores asignados y ve en vivo quién trabaja. Sesiones de revisión en lote, desde móvil o desktop.
- **Admin:** gobierna usuarios, asignaciones y la matriz de permisos, y monitorea todos los equipos.
- **Audiencia real hoy:** un entrevistador o reclutador técnico que evalúa el proyecto de portafolio. Abre la demo, cambia de rol con el dropdown del login y prueba los flujos en tiempo real (a menudo con dos navegadores lado a lado).

## Product Purpose

Registro y aprobación de horas con visibilidad en tiempo real y decisiones a prueba de carreras: ningún registro se aprueba dos veces aunque dos supervisores decidan al mismo tiempo. El éxito se mide en que el revisor técnico entienda en minutos el flujo completo (marcar → registro → decisión → notificación) y vea la concurrencia resuelta correctamente.

## Positioning

Proyecto de portafolio que se ve y se comporta como un producto real: presencia en vivo entre réplicas, conflictos 409 explicados al usuario ("Ya fue aprobado por X"), permisos editables que fuerzan el relogin, y una demo que se reinicia sola.

## Operating Context

- Demo pública con datos generados (Bogus) que se resetean cada 24 h (configurable). Después de un reset, todos deben volver a loguearse.
- El login tiene un dropdown de rol que llena credenciales de demo.
- Uso típico de la evaluación: dos ventanas, el trabajador marca y el supervisor lo ve aparecer, y dos supervisores aprueban a la vez.

## Capabilities and Constraints

- Estados del registro: Pendiente, Aprobado (final), En revisión (devuelto con razón), Rechazado (final). Historial por registro.
- Origen del registro: Punch o Manual (los manuales se marcan visiblemente).
- Presencia: Trabajando (punch abierto) / Online / Offline.
- Las razones escritas por usuarios no se traducen. Los errores de la API llegan como códigos que traduce el frontend.
- Zona horaria por usuario; las horas se muestran en la zona del trabajador.
- Sin dinero ni nómina.

## Brand Commitments

Nombre: **Worktime**. Sin logo ni assets previos.

## Evidence on Hand

No hay clientes, testimonios ni métricas reales; no se deben inventar. Todos los datos son de demo.

## Product Principles

1. El estado siempre es inequívoco: cada registro y cada persona muestra su estado actual sin ambigüedad.
2. Lo que pasó en otro lado se ve aquí: los cambios de otros llegan en vivo y los conflictos se explican, no se esconden.
3. Las decisiones dejan rastro: toda transición tiene autor, momento y razón visibles.
4. Igual de usable en móvil y en desktop para todos los roles.

## Accessibility & Inclusion

WCAG 2.2 AA. El estado nunca se comunica solo con color (siempre va con etiqueta o ícono). Navegación completa por teclado en tablas, matriz y diálogos (CDK).
