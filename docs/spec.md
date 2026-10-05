---
title: Worktime — registro y aprobación de horas en tiempo real
labels: [ready-for-agent]
---

## Problem Statement

Los trabajadores registran sus horas de forma dispersa y no saben en qué estado está cada registro (aprobado, devuelto para corrección o rechazado) ni por qué. Los supervisores no tienen una vista en vivo de quién está trabajando ni de quién tiene la app abierta, y aprueban horas sin coordinarse entre ellos. Dos supervisores (o un supervisor y un admin) pueden decidir sobre el mismo registro al mismo tiempo y aprobarlo dos veces. Los trabajadores que se quedan sin supervisor tampoco tienen una vía clara para pedir uno. El admin no tiene forma de gobernar quién puede hacer qué, ni de supervisar los equipos.

## Solution

Una plataforma web (Angular + ASP.NET Core 8 + PostgreSQL + Redis + SignalR) con tres roles:

- **Trabajador:** marca punch-in y punch-out desde la app (cada punch genera un registro de horas), crea registros manuales, ve sus horas del mes con el estado de cada registro y su historial, edita con una razón los registros pendientes o devueltos, y pide asignación de supervisor si no tiene uno.
- **Supervisor:** ve en vivo a sus trabajadores (trabajando, online u offline), aprueba, devuelve con razón o rechaza registros (uno a uno o en lote), y administra a sus trabajadores (crear, editar, desactivar, desvincular).
- **Admin:** administra supervisores y trabajadores, asigna y reasigna, resuelve solicitudes de asignación, monitorea todos los equipos y los trabajadores huérfanos, y edita la matriz rol × permiso.

Ningún registro se decide dos veces, aunque las decisiones lleguen al mismo tiempo a réplicas distintas. La demo se reinicia con datos de prueba en un intervalo configurable y fuerza el relogin.

## User Stories

### Autenticación y sesión
1. Como usuario, quiero loguearme con email y contraseña, para acceder a las funciones de mi rol.
2. Como visitante de la demo, quiero un dropdown de rol en el login que llene credenciales de demo, para probar cada rol sin conocer contraseñas.
3. Como usuario, quiero que el rol elegido en el dropdown no otorgue privilegios, para que el rol real siempre salga del servidor.
4. Como usuario, quiero que mi sesión dure una jornada (8 h), para no tener que reloguearme durante el día.
5. Como usuario, quiero ser expulsado con el mensaje "La demo se reinició" cuando la demo se resetea, para entender por qué tengo que volver a entrar.
6. Como usuario, quiero ser expulsado con el mensaje "Tus permisos cambiaron" cuando el admin modifica la matriz de mi rol, para recibir mis nuevos permisos.
7. Como usuario desactivado, quiero perder el acceso de inmediato, incluida la conexión en tiempo real.
8. Como operador, quiero que el login tenga rate limit por IP, para frenar ataques de fuerza bruta.
9. Como usuario, quiero cambiar el idioma entre español e inglés, para usar la app en mi idioma.
10. Como usuario, quiero ver si mi conexión en tiempo real está activa o reconectando, para saber si lo que veo está al día.

### Trabajador: punch
11. Como trabajador, quiero hacer punch-in con un botón, para registrar el inicio de mi jornada.
12. Como trabajador, quiero ver un cronómetro en vivo desde mi punch-in, para saber cuánto llevo trabajado.
13. Como trabajador, quiero hacer punch-out y que se cree automáticamente un registro pendiente con inicio y fin, para no tener que escribirlo a mano.
14. Como trabajador, quiero que un doble clic o una segunda pestaña no abran dos punch-in, para no generar registros duplicados.
15. Como trabajador, quiero que un turno que cruza la medianoche local se divida en dos registros, para que cada día sume correctamente.
16. Como trabajador, quiero que mi supervisor vea al instante que empecé o terminé de trabajar.

### Trabajador: registros
17. Como trabajador, quiero crear un registro manual cuando olvidé marcar, para no perder horas trabajadas.
18. Como trabajador, quiero que no se acepten registros manuales en el futuro ni con más de 31 días de antigüedad.
19. Como trabajador, quiero que se rechacen registros que se solapan con otros míos, para no duplicar horas.
20. Como trabajador, quiero ver un calendario mensual con las horas de cada día coloreadas por estado, para entender mi mes de un vistazo.
21. Como trabajador, quiero ver los totales del mes por estado (aprobadas, pendientes, en revisión, rechazadas).
22. Como trabajador, quiero ver el historial de cada registro (quién, cuándo, transición y razón), para saber por qué me lo devolvieron o rechazaron.
23. Como trabajador, quiero editar un registro pendiente o devuelto indicando una razón, para corregirlo, y que vuelva a pendiente.
24. Como trabajador, quiero que los registros aprobados y rechazados no se puedan editar.
25. Como trabajador, quiero recibir al instante el cambio de estado de mis registros.
26. Como trabajador, quiero ver las horas en mi zona horaria.

### Trabajador: asignación
27. Como trabajador, quiero ver quién es mi supervisor o que no tengo uno.
28. Como trabajador sin supervisor, quiero poder seguir trabajando y registrando horas.
29. Como trabajador sin supervisor, quiero enviar al admin una solicitud de asignación con una nota y un supervisor sugerido.
30. Como trabajador, quiero que no se me permita tener dos solicitudes pendientes.
31. Como trabajador, quiero recibir al instante la resolución de mi solicitud (asignado o descartado con razón).

### Supervisor: equipo en vivo
32. Como supervisor, quiero ver una tarjeta por trabajador con su estado: trabajando desde HH:MM, online sin marcar u offline.
33. Como supervisor, quiero ver las horas del mes y los pendientes de cada trabajador.
34. Como supervisor, quiero que la presencia se actualice en vivo sin recargar.
35. Como supervisor, quiero que un trabajador cuya conexión se cayó (por ejemplo, porque murió la réplica) aparezca offline en menos de un minuto.
36. Como supervisor, quiero que un trabajador con varias pestañas no parpadee como offline al cerrar una.

### Supervisor: aprobación
37. Como supervisor, quiero una bandeja con los registros pendientes de mis trabajadores, con filtros por trabajador y fecha.
38. Como supervisor, quiero aprobar un registro, con una razón opcional.
39. Como supervisor, quiero pedir revisión con una razón obligatoria.
40. Como supervisor, quiero rechazar con una razón obligatoria.
41. Como supervisor, quiero ver si un registro es manual o de punch, para revisar con más cuidado los manuales.
42. Como supervisor, quiero que si otro supervisor o el admin decide primero, mi intento falle con el mensaje "Ya fue decidido por X" en lugar de duplicar la decisión.
43. Como supervisor, quiero que la fila desaparezca o se actualice en vivo cuando otro decide sobre ella.
44. Como supervisor, quiero aprobar en lote y recibir un resultado parcial ("7 aprobados, 3 ya decididos").
45. Como supervisor, quiero que se me impida aprobar registros de trabajadores que ya no son míos, aunque la desvinculación ocurra en el mismo instante.
46. Como supervisor, quiero que se me impida aprobar mis propios registros.
47. Como supervisor, quiero ver las horas en la zona del trabajador, con una indicación si difiere de la mía.

### Supervisor: gestión de trabajadores
48. Como supervisor, quiero crear trabajadores, que quedan asignados a mí.
49. Como supervisor, quiero editar el nombre y el email de mis trabajadores.
50. Como supervisor, quiero desactivar a un trabajador mío sin borrar su historial.
51. Como supervisor, quiero desvincular a un trabajador, dejándolo sin supervisor y activo.
52. Como supervisor, quiero no ver ni poder tocar a los trabajadores de otros ni a los huérfanos.
53. Como supervisor, quiero que un trabajador asignado a mí aparezca en mi panel al instante.

### Admin
54. Como admin, quiero hacer CRUD de supervisores y trabajadores (con desactivar en lugar de eliminar).
55. Como admin, quiero reactivar usuarios desactivados.
56. Como admin, quiero asignar, reasignar y desvincular trabajadores de supervisores.
57. Como admin, quiero que al reasignar, los pendientes del trabajador pasen a la bandeja del nuevo supervisor.
58. Como admin, quiero monitorear todos los equipos agrupados por supervisor, en vivo.
59. Como admin, quiero una bandeja de trabajadores huérfanos con sus pendientes, y poder decidir sobre ellos.
60. Como admin, quiero una bandeja en vivo de solicitudes de asignación, con el supervisor sugerido precargado.
61. Como admin, quiero resolver una solicitud asignando un supervisor o descartándola con una razón.
62. Como admin, quiero que una solicitud pendiente se cierre sola si asigno al trabajador por otra vía.
63. Como admin, quiero editar la matriz rol × permiso partiendo de valores por defecto.
64. Como admin, quiero que ciertas celdas estén bloqueadas (por ejemplo, la gestión de permisos y de usuarios del Admin), para no quedarme sin acceso.
65. Como admin, quiero que antes de guardar se me avise cuántos usuarios tendrán que reloguearse.

### Operación
66. Como operador, quiero levantar todo con un solo `docker compose up`.
67. Como operador, quiero que la demo se resetee con datos realistas en un intervalo configurable (24 h por defecto), y poder desactivarlo.
68. Como operador, quiero que con varias réplicas el reset y el barrido de presencia corran en una sola.
69. Como operador, quiero logs JSON estructurados con un correlation id por request.
70. Como operador, quiero un log de warning para las requests lentas, con un umbral configurable.
71. Como operador, quiero métricas de duración de requests, aprobaciones, conflictos de concurrencia y conexiones SignalR.
72. Como operador, quiero un endpoint de health que verifique Postgres y Redis.
73. Como operador, quiero configurar el TTL del caché del resumen de equipo (5 s por defecto).

## Implementation Decisions

### Despliegue
- `docker compose` con postgres, redis, dos réplicas de la API (misma imagen) y nginx. nginx hace round-robin entre las réplicas con WebSocket upgrade y sirve los builds de Angular en `/es/` y `/en/`. El cliente de SignalR usa solo WebSockets con skipNegotiation (sin sticky sessions).
- Las réplicas corren con workstation GC para limitar la RAM.

### Estructura
- Cuatro proyectos por capa: Domain (sin dependencias), Application (casos de uso y puertos), Infrastructure (EF Core/Npgsql, Redis, seeder, background services) y Api (controllers, hub, middleware, auth).
- Dentro de cada proyecto hay carpetas por módulo: **Users**, **Permissions**, **Punch**, **WorkLogs** y **Assignments**. Cada módulo expone su registro de DI (`AddXModule`). Hay un solo DbContext con configuraciones por entidad.
- CQRS ligero con la librería **Mediator** (martinothamar, source-generated). Los commands pasan por los agregados; las queries proyectan directo a DTO con AsNoTracking. Hay dos pipeline behaviors: logging por caso de uso y validación.
- Los agregados acumulan eventos de dominio. El unit of work los despacha a un puerto de notificación (implementado con SignalR) **solo después** de un save exitoso.
- Puertos de Application: repositorios por agregado, unit of work, notifier, presence store, cache, clock, password hasher y token issuer.
- Async de punta a punta con CancellationToken. SOLID.

### Modelo de dominio
- **User:** rol (Worker/Supervisor/Admin), SecurityStamp, IsActive, SupervisorId (solo para trabajadores), TimeZone IANA y hash de la contraseña. Los usuarios nunca se eliminan, solo se desactivan. Desactivar, cambiar el rol o cambiar la matriz del rol regenera el stamp. Desvincular no lo regenera.
- **WorkLog:** StartAt/EndAt (timestamptz UTC), Source (Punch|Manual), Note y Status, con historial de **WorkLogEvent** (actor, momento, de→a, razón). Máquina de estados:

  ```
  Pending --Approve(reason?)--> Approved        (final)
  Pending --Reject(reason)----> Rejected        (final)
  Pending --RequestRevision(reason)--> NeedsRevision
  Pending|NeedsRevision --Edit(reason) [solo el dueño]--> Pending
  ```
  Invariantes: EndAt > StartAt; duración ≤ 16 h; no cruza la medianoche local del trabajador; nadie decide sobre sus propios registros; los manuales no pueden estar en el futuro ni tener más de 31 días.
- **PunchSession:** abierta o cerrada. Al cerrarse produce uno o dos WorkLogs (si cruza la medianoche).
- **AssignmentRequest:** trabajador, nota, supervisor sugerido, estado (Pending/Fulfilled/Dismissed), quién la resolvió, cuándo y la razón. Solo la puede crear un trabajador sin supervisor.
- **Catálogo de permisos** fijo en código y **RolePermission** (matriz editable) con valores por defecto sembrados y celdas bloqueadas para Admin.
- Quién puede decidir sobre un registro se calcula en el momento de la decisión: el supervisor actual del trabajador o cualquier admin. No hay dinero ni tarifas.

### Concurrencia (en capas)
1. **Dominio:** las transiciones inválidas lanzan una excepción de dominio (422).
2. **Por instancia:** un `ConcurrentDictionary<Guid, SemaphoreSlim>` serializa las decisiones sobre el mismo registro dentro del proceso. Es una optimización, no la garantía.
3. **DB:** `xmin` como token de concurrencia en WorkLog y User. Un conflicto se traduce en 409 con un código estable.
- **Aprobar vs. desvincular:** el handler lee al trabajador con `FOR SHARE` en la misma transacción que actualiza el registro.
- **Constraints en Postgres:** partial unique (una sola PunchSession abierta por trabajador), partial unique (una sola AssignmentRequest pendiente por trabajador), y exclusion constraint gist sobre (trabajador, tstzrange) para registros no rechazados (sin solapamientos).
- La aprobación en lote procesa cada registro por separado y devuelve un resultado por id (ok / conflicto / prohibido).

### Auth
- Contraseñas con `PasswordHasher<User>`, sin ASP.NET Identity.
- JWT de 8 h, sin refresh token, guardado en localStorage. Interceptor HTTP para el header Bearer; SignalR usa accessTokenFactory, y el servidor lee `access_token` del query string solo en las rutas del hub.
- Claims: `sub`, `name`, `email`, `role`, `stamp`, varios `perm` y `supervisor_id` (solo informativo).
- Al validar el token se compara `stamp` con el valor en Redis (cacheado brevemente en memoria). Si no coincide, responde 401.
- Atributo `RequirePermission` derivado de AuthorizeAttribute + un policy provider que construye políticas `perm:*` en el momento. Se aplica a controllers y a métodos del hub. La autorización por propiedad del recurso vive en el dominio/handlers.
- Guardar la matriz regenera el stamp de los usuarios de los roles afectados y emite ForceLogout.
- Rate limiter nativo en el login: fixed window de 10/min por IP.

### Tiempo real y caché
- Grupos de SignalR: `user-{id}`, `supervisor-{id}` y `admins`. Backplane en Redis.
- Eventos: PunchStarted/PunchEnded, PresenceChanged, WorkLogSubmitted/Edited, WorkLogStatusChanged, AssignmentRequested, AssignmentResolved, WorkerAssigned/Unassigned, ForceLogout y DemoReset.
- **Presencia:** sorted set en Redis (userId → último heartbeat) con heartbeat del cliente cada 30 s; online significa < 60 s. Un contador de conexiones por usuario evita el parpadeo. El barrido cada 30 s emite los offline expirados, bajo advisory lock.
- **Caché distribuido** solo para la query del resumen de equipo, con TTL configurable (5 s por defecto) y sin invalidación por eventos. Los eventos envían deltas, y el resumen completo solo se pide al cargar o reconectar. Las decisiones nunca leen del caché.

### Pipeline de middleware (en orden)
ExceptionHandler nativo (DomainException→422, conflicto→409, prohibido→403, resto→500, todos como ProblemDetails con un código estable) → CorrelationId → RequestTiming (Stopwatch, log estructurado, warning sobre el umbral configurable, Histogram) → CORS → RateLimiter → Authentication → Authorization → endpoints/hub. Más un filtro del hub para medir el tiempo de cada invocación.
- Logging nativo con formatter JSON. Métricas con System.Diagnostics.Metrics. Health checks para Postgres y Redis.

### Seed y reset
- Seeder en runtime con **Bogus**: 1 admin, 2 supervisores, ~8 trabajadores y ~5 semanas de registros relativos a la fecha actual, en todos los estados y con historial coherente, más algunos huérfanos y solicitudes. Ids aleatorios.
- Un BackgroundService con PeriodicTimer (`Seed:Enabled`, `Seed:ResetInterval` = 24 h por defecto): toma el advisory lock, trunca, vuelve a sembrar en una transacción, restablece la matriz por defecto y emite DemoReset. Toda la configuración va en appsettings/variables de entorno.
- Endpoint público con las cuentas de demo (solo si el seed está habilitado), para el dropdown del login.

### API (contratos principales)
- Auth: login y cuentas de demo.
- Punch: punch-in, punch-out y estado actual.
- WorkLogs: crear manual, editar, listar los míos por mes con historial, bandeja de pendientes, approve / request-revision / reject y approve en lote.
- Team: resumen (cacheado).
- Users: CRUD con alcance según el rol, desactivar/reactivar, asignar/desvincular.
- Assignments: crear solicitud, listar pendientes, fulfill/dismiss.
- Permissions: leer y guardar la matriz (con el número de usuarios afectados).
- Hub: Heartbeat.

### Frontend
- Angular 22: standalone, signals, zoneless, lazy routes por rol y guards por permiso (solo para la UX).
- Un servicio de realtime con reconexión y re-fetch al reconectar.
- Angular CDK + Tailwind v4 con tokens en `@theme` (incluidos los colores de estado).
- i18n nativa (`@angular/localize`) con español por defecto e inglés, un build por locale. La API devuelve códigos y Angular los traduce; el contenido escrito por los usuarios no se traduce.
- Pantallas: Hoy y Mes (trabajador); Equipo en vivo, Bandeja y Mis trabajadores (supervisor); Monitoreo + huérfanos, Usuarios, Solicitudes y Matriz (admin); más el login, el selector de idioma, los toasts y el indicador de conexión.

### Docs
- README con las decisiones, la explicación de la concurrencia, el escalado y el consumo de memoria, y un diagrama de arquitectura en Excalidraw.

## Testing Decisions

- **Un buen test verifica comportamiento observable a través de la interfaz pública** (estado resultante, eventos emitidos, respuesta HTTP), no detalles internos como qué método privado se llamó o en qué orden.
- **Seams (de más alto a más bajo):**
  1. **API real sobre Postgres y Redis reales** (WebApplicationFactory + Testcontainers): solo para lo que no se puede probar sin infraestructura real. Doble aprobación ×10 en paralelo desde contextos distintos (exactamente 1 éxito), aprobar vs. desvincular en paralelo, doble punch-in rechazado por el constraint, y token con stamp viejo → 401.
  2. **Handlers de Application con los puertos mockeados (NSubstitute):** es el seam principal. Por ejemplo: los eventos solo se notifican si el save tuvo éxito, el conflicto del unit of work se propaga como conflicto, la query de resumen usa el caché, cambiar la matriz regenera los stamps, una solicitud pendiente se cierra al asignar por otra vía, y la aprobación en lote devuelve resultados parciales.
  3. **Agregados de Domain, puros y sin mocks:** todas las transiciones válidas e inválidas de WorkLog, razones obligatorias, prohibición de la auto-aprobación, partición en la medianoche y límites de duración, PunchSession, AssignmentRequest y celdas bloqueadas de la matriz.
- xUnit + NSubstitute, con las aserciones de xUnit. El reloj se inyecta para tener tiempo determinista.
- **Prior art:** ninguno; el repo es nuevo. Estos tests establecen el patrón.

## Out of Scope

- Tests del frontend de Angular (anotado para después).
- OpenTelemetry/Prometheus/Grafana (anotado para después; los instrumentos actuales son compatibles).
- Dinero, tarifas y nómina.
- Refresh tokens, cookies httpOnly, 2FA, recuperación de contraseña y confirmación de email.
- Usuarios con varios roles.
- Preferencia de refresco o TTL de caché por usuario.
- Cambio de idioma en vivo sin recargar.
- Eliminación física de usuarios.
- Módulos como proyectos separados o microservicios, y tests de arquitectura.

## Further Notes

- Este spec viene de una sesión de grilling que reconstruye un caso de entrevista. El objetivo es demostrar la concurrencia correcta (lock por instancia como optimización, la DB como garantía), autorización por claims vs. por propiedad, y tiempo real distribuido.
- Memoria estimada: ~0.4–0.55 GB el stack del compose y ~1.5–2 GB con Docker Desktop. Se recomienda limitar WSL2 a 4 GB.
- El repo no tiene remoto todavía. Cuando exista, publicar este spec como issue con la etiqueta `ready-for-agent`.
