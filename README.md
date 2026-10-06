# Worktime

Registro y aprobación de horas en tiempo real. **Angular 22 + SignalR + ASP.NET Core 8 + PostgreSQL + Redis**.

Tres roles: el **trabajador** marca entrada y salida y corrige sus registros con una razón; el **supervisor** ve en vivo quién trabaja y decide sobre las horas de su equipo; el **admin** gobierna usuarios, asignaciones y la matriz de permisos. El caso central es **"dos supervisores aprueban el mismo registro al mismo tiempo"**: está resuelto en capas y demostrado con tests sobre Postgres real.

- Spec completo: [`docs/spec.md`](docs/spec.md)
- Diagrama: [`docs/architecture.excalidraw`](docs/architecture.excalidraw) (ábrelo en <https://excalidraw.com>)
- Producto y sistema visual: [`PRODUCT.md`](PRODUCT.md) y [`DESIGN.md`](DESIGN.md)

## Levantarlo

Requiere Docker Desktop (WSL2 en Windows).

```bash
docker compose up --build
# → http://localhost:8080   (redirige a /es/ o /en/ según el navegador)
```

En el login, el dropdown **Cuenta de demo** llena las credenciales (contraseña `Demo1234!`):

| Cuenta | Para ver |
|---|---|
| `worker1@worktime.demo` | Ya está marcando: reloj, cronómetro y pista del día |
| `worker2@worktime.demo` | Registros devueltos para corregir |
| `worker8@worktime.demo` | Sin supervisor, con una solicitud pendiente |
| `supervisor1@worktime.demo` | Equipo en vivo y bandeja de aprobación |
| `admin@worktime.demo` | Todos los equipos, huérfanos, usuarios, solicitudes y matriz |

**Demo de concurrencia a mano:** abre dos navegadores con `supervisor1` (o `supervisor1` + `admin`), elige el mismo registro y apruébalo en los dos a la vez. Uno gana; el otro ve *"Otra persona ya decidió este registro"*, y la fila muestra quién lo decidió.

### Desarrollo sin reconstruir imágenes

```bash
docker compose up postgres redis          # solo infraestructura
dotnet run --project src/Worktime.Api     # http://localhost:5080 (migra y siembra al arrancar)
cd web && npm start                       # http://localhost:4200, proxy /api y /hubs → :5080
```

### Configuración (`appsettings.json` o variables de entorno)

| Clave | Por defecto | Qué hace |
|---|---|---|
| `Seed:Enabled` | `true` | Siembra datos de demo y activa el reset periódico |
| `Seed:ResetInterval` | `24:00:00` | Cada cuánto se borra y se vuelve a sembrar la demo |
| `Cache:TeamSummarySeconds` | `5` | TTL del resumen de equipo en Redis |
| `Diagnostics:SlowRequestMs` | `500` | Umbral del warning de request lenta |
| `Jwt:SigningKey` | (dev) | Clave HMAC compartida por todas las réplicas |
| `Jwt:LifetimeHours` | `8` | Duración del token (una jornada) |

En compose: `SEED_RESET_INTERVAL`, `TEAM_SUMMARY_CACHE_SECONDS`, `JWT_SIGNING_KEY` y `POSTGRES_PASSWORD`. Si los puertos chocan con servicios locales, cámbialos con `WORKTIME_PORT` (8080), `POSTGRES_PORT` (5432) y `REDIS_PORT` (6379). Postgres y Redis se publican solo en `127.0.0.1`.

## Deploy continuo (Jenkins)

El `Jenkinsfile` de este repo lo ejecuta el Jenkins self-hosted del repo hermano [`jenkins-local`](../jenkins-local), que hace polling de `main` cada 2 minutos:

1. **Tests:** los 47, incluidos los de integración con Testcontainers, en un contenedor del SDK.
2. **Imágenes y release:** `worktime-api:<build>-<sha>` y `worktime-web:<build>-<sha>`, más un *release* con el `docker-compose.yml` y el script de esa versión.
3. **Deploy rolling** ([`deploy/rolling-deploy.sh`](deploy/rolling-deploy.sh)), con [`docker-compose.server.yml`](docker-compose.server.yml) encima: en el servidor Postgres es el compartido de jenkins-local, no el de este compose. actualiza api-1, espera a que esté healthy, sigue con api-2 y luego nginx. nginx re-resuelve el DNS de las réplicas (`resolve`) y salta en 2 s a la otra si una no responde.
4. **Smoke test** por nginx.
5. **Rollback automático** si algo falla, a la última versión buena, **con su configuración**. Para rollback manual: *Build with Parameters* → `DEPLOY_TAG`.

## Arquitectura

```
src/
  Worktime.Domain          agregados e invariantes, sin dependencias
  Worktime.Application     casos de uso (CQRS con Mediator), puertos
  Worktime.Infrastructure  EF Core/Npgsql, Redis, seeder (Bogus), jobs
  Worktime.Api             controllers, hub, middleware, JWT
tests/
  Worktime.Domain.Tests        puros, sin mocks
  Worktime.Application.Tests   handlers con puertos mockeados (NSubstitute)
  Worktime.IntegrationTests    API real + Postgres/Redis reales (Testcontainers)
web/                           Angular
```

Es un **monolito modular**: los proyectos van por capa y las carpetas por módulo (`Users`, `Permissions`, `Punch`, `WorkLogs`, `Assignments`, `Team`). Cada módulo registra lo suyo con `AddXModule()`, y `Program.cs` los compone en orden. Hay un solo `DbContext`, porque la aprobación necesita el registro y el trabajador **en la misma transacción**.

**CQRS ligero:** los commands pasan por los agregados; las queries proyectan directo a DTOs con `AsNoTracking`. Se usa [Mediator](https://github.com/martinothamar/Mediator) (source generator, MIT) en lugar de MediatR, que desde la v13 tiene licencia comercial. Los behaviors (`LoggingBehavior` y `ValidationBehavior`) cubren lo transversal de cada caso de uso.

**Eventos de dominio → tiempo real:** los agregados acumulan eventos, el `UnitOfWork` los despacha **después del commit**, y `RealtimeEventDispatcher` los traduce a mensajes de SignalR. Ningún cliente se entera de un cambio que se revirtió.

## Doble aprobación: defensa en capas

| Capa | Mecanismo | Qué garantiza |
|---|---|---|
| Instancia | `KeyedLock`: `ConcurrentDictionary<Guid, SemaphoreSlim>` por registro | Dentro del proceso, las decisiones sobre el mismo registro hacen cola en lugar de ir todas a la DB. **Es una optimización, no la garantía.** |
| Transacción | `SELECT … FOR SHARE` sobre la fila del trabajador | "Aprobar mientras me desvinculan" se resuelve en un solo orden: la desvinculación espera, o la aprobación lee `supervisor_id = null` → 403 |
| Dominio | `WorkLog.Decide()` solo acepta `Pending` | Reglas: `Approved` y `Rejected` son finales, nadie decide sobre sus propios registros, razón obligatoria |
| Base de datos | `xmin` como token de concurrencia (EF `IsRowVersion`) | Entre réplicas, el `UPDATE … WHERE xmin = @v` del perdedor afecta 0 filas → **409** `concurrency.stale` |

Un `CHECK` constraint no sirve para esto, porque valida un valor y no una transición. Donde **sí** gana el constraint es en otros tres casos, todos con 409:

- `UNIQUE (worker_id) WHERE ended_at IS NULL`: un solo punch abierto (doble clic, dos pestañas).
- `UNIQUE (worker_id) WHERE status = 'Pending'`: una sola solicitud de supervisor pendiente.
- `EXCLUDE USING gist (worker_id WITH =, tstzrange(start_at, end_at) WITH &&) WHERE status <> 'Rejected'`: sin horas solapadas.

`ConcurrencyTests` lanza 10 aprobaciones simultáneas desde 10 "réplicas" (cada una con su scope, su `DbContext` y su `KeyedLock`, sin serialización en memoria) y verifica que se confirma **exactamente una**.

## Auth: JWT + claims + security stamp

- `User` propio + `PasswordHasher<User>` (PBKDF2), sin ASP.NET Identity.
- El JWT dura 8 h y lleva `sub`, `role`, `stamp`, varios `perm` y `supervisor_id` (este último solo para la UI). Se guarda en `localStorage`, se envía como Bearer y SignalR lo pasa en `access_token` (solo en `/hubs`).
- **Sin refresh token, a propósito:** la revocación es inmediata gracias al **security stamp**. `OnTokenValidated` compara el claim `stamp` con Redis (con 2 s de caché en memoria y fallback a Postgres). El stamp cambia al resetear la demo, al desactivar un usuario y al cambiar su rol en la matriz, y entonces responde 401. Además, `ForceLogout` por SignalR cierra la UI al instante y `HubGuardFilter` vuelve a validar en cada invocación al hub.
- **Capacidad vs. propiedad:** `[RequirePermission(Perms.WorkLogsApprove)]` es un `AuthorizeAttribute` con un `IAuthorizationPolicyProvider` que arma políticas `perm:*` al vuelo. Corre antes del model binding, devuelve 401/403 correctos y sirve igual en controllers y en el hub. *"¿Es tu trabajador?"* no es una capacidad, y se decide en el dominio y los handlers.
- **Matriz editable:** el catálogo de permisos es fijo en código; el admin activa celdas rol × permiso. `Admin/permissions:manage` y `Admin/users:manage` están **bloqueadas** para que nadie quede fuera. Guardar rota el stamp de los usuarios de cada rol cambiado, así que vuelven a entrar con los claims nuevos.
- **Trade-off de `localStorage`:** queda expuesto a XSS. Se mitiga con la sanitización por defecto de Angular y se puede endurecer con CSP. Las cookies httpOnly eliminarían ese riesgo, pero agregarían CSRF y ya no sería "bearer".

## Tiempo real, presencia y caché

- **Backplane Redis:** un evento emitido en api-1 llega a los clientes conectados a api-2. nginx balancea en round-robin **sin sticky sessions**, porque el cliente usa solo WebSockets con `skipNegotiation`.
- **Presencia:** un ZSET guarda el último heartbeat de cada usuario (cada 30 s; se considera online si fue hace menos de 60 s) y un contador de conexiones evita el parpadeo al cerrar una pestaña. Si una réplica muere, sus usuarios **expiran solos**: un barrido cada 30 s, bajo advisory lock, emite "offline".
- **Caché de 5 s** solo para `GET /api/team/summary` (el agregado caro que todos piden al reconectar). No se invalida: los eventos llevan deltas. **Las decisiones nunca leen del caché.**

## Pipeline de middleware

```
ForwardedHeaders → ExceptionHandler (ProblemDetails + code) → CorrelationId → RequestTiming (Stopwatch)
→ CORS → Authentication (JWT + stamp) → RateLimiter (login 10/min/IP) → Authorization (perm:*) → endpoints/hub
```

- Los logs son JSON por consola con el `CorrelationId` en cada línea, y hay un warning sobre `SlowRequestMs`.
- Métricas nativas (`System.Diagnostics.Metrics`, meter `Worktime`): `http.server.request.duration_ms`, `worklogs.decisions`, `worklogs.concurrency_conflicts` y `signalr.connections`. Para verlas: `docker compose exec api-1 dotnet-counters monitor -n Worktime.Api --counters Worktime` (con dotnet-counters instalado).
- `/health` verifica Postgres y Redis.

## Datos de demo

`DemoSeeder` (Bogus, locale `es`) genera 1 admin, 2 supervisores y 9 trabajadores (uno sin supervisor, uno desactivado), con unas 5 semanas de registros **relativos a hoy** en todos los estados, historial coherente, dos personas marcando en este momento y una solicitud pendiente. Los ids son aleatorios: tras cada reset los tokens viejos apuntan a nadie, y el stamp fuerza el relogin.

`DemoResetService` corre cada `Seed:ResetInterval`. El advisory lock más el chequeo "¿los datos siguen frescos?" garantizan que **una sola réplica** resetee por intervalo.

## Tests

```bash
dotnet test tests/Worktime.Domain.Tests
dotnet test tests/Worktime.Application.Tests
dotnet test tests/Worktime.IntegrationTests     # requiere Docker (Testcontainers)
```

## Memoria aproximada

Medido con `docker stats` con el stack en reposo después del seed:

| Componente | RAM medida |
|---|---|
| api-1 / api-2 (workstation GC, `mem_limit` 320 MB) | ~110 MB / ~105 MB |
| Postgres (`mem_limit` 256 MB) | ~44 MB |
| nginx | ~15 MB |
| Redis (`maxmemory` 64 MB) | ~6 MB |
| **Stack del compose** | **~280 MB** |
| Docker Desktop (VM de WSL2, en reposo) | ~1.0–1.5 GB (estimado) |
| **Total con Docker Desktop** | **~1.3–1.8 GB** |

Para limitar la VM, crea `%UserProfile%\.wslconfig` con `[wsl2]` y `memory=4GB`.

## Escalado

- Más réplicas: agrega `api-3` en el compose y en el `upstream` de nginx. Backplane, presencia, caché, stamps y jobs ya son compartidos.
- `KeyedLock` sigue siendo por instancia, y está bien así: la garantía la da `xmin`.
- El resumen de equipo agrega en memoria unas 5 semanas. Con equipos grandes conviene llevarlo a SQL por zona horaria (marcado con `ponytail:` en el código).

## Futuro

- OpenTelemetry → Prometheus + Grafana. Los instrumentos actuales se exportan sin cambios, solo agregando el exporter.
- Tests del frontend Angular.
