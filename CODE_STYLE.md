# Code style (backend .NET)

El código se lee muchas más veces de las que se escribe. Preferimos archivos cortos que se entienden de un vistazo antes que pocos archivos grandes.

## Un tipo por archivo

- Cada tipo top-level (clase, record, interfaz, enum) va en **su propio archivo**, con el mismo nombre que el tipo: `WorkLogDto` → `WorkLogDto.cs`. Si es genérico, el archivo no lleva los parámetros: `ValidationBehavior<,>` → `ValidationBehavior.cs`.
- **Excepción:** un command o query comparte archivo con su handler (`CreateManualWorkLogCommand.cs` contiene el record y `CreateManualWorkLogHandler`). El record es una línea y casi siempre se lee junto a su handler.
- Los tipos **anidados** pequeños pueden quedarse en el archivo de su tipo contenedor. Por ejemplo, los request records de cada controller (`AuthController.LoginRequest`) son el contrato privado de ese controller.

## Carpetas por feature

Primero se organiza por feature y, dentro de cada feature, por tipo de pieza.

```
Worktime.Domain/WorkLogs/
├─ Events/        WorkLogSubmitted.cs, …        (domain events)
├─ WorkLog.cs                                   (aggregate)
└─ WorkLogStatus.cs, Decision.cs, …             (enums, value types)

Worktime.Application/WorkLogs/
├─ Commands/      CreateManualWorkLogCommand.cs (+ handler)
├─ Queries/       MyMonthQuery.cs (+ handler)
├─ Results/       WorkLogDto.cs, BatchItemResult.cs, BatchOutcome.cs
├─ Interfaces/    IWorkLogRepository.cs, IWorkLogReads.cs
└─ WorkLogDecider.cs                            (helpers de la feature)

Worktime.Infrastructure/WorkLogs/
├─ WorkLogsModule.cs                            (solo registro DI)
├─ WorkLogConfiguration.cs                      (EF)
├─ WorkLogRepository.cs
└─ WorkLogReads.cs

Worktime.Api/Controllers/
└─ WorkLogsController.cs
```

- **Results/**: todo lo que devuelve un command o query (`*Dto`, `*Result`, `*Row`, `*Payload`), junto con los enums que solo existen dentro de esos resultados.
- **Interfaces/**: las interfaces **nunca** se declaran junto a su implementación ni junto a quien las usa. Las de una feature van en `<Feature>/Interfaces/`; las transversales, en `Common/Interfaces/`.
- Lo que no es command, query, result ni interfaz va en la raíz de la feature.
- Los nombres no cambian solo para cumplir la carpeta: un `*Dto` en `Results/` sigue llamándose `*Dto`.

## Namespaces = carpetas

El namespace siempre coincide con la carpeta, aunque resulte redundante (`Worktime.Application.WorkLogs.Commands`). Así el `using` muestra de dónde viene cada tipo. Usamos `using` explícitos por archivo, sin `global using` de namespaces propios, y sin `using` sin usar.

## Program con `Main` explícito

Nada de top-level statements. `Program` es una clase (`public`, **no** `static`, porque `WebApplicationFactory<Program>` la usa como argumento genérico) y su `Main` se lee como un índice:

```csharp
public static void Main(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);
    AddModules(builder.Services, builder.Configuration);
    AddAuth(builder.Services, builder.Configuration);
    …
    var app = builder.Build();
    UsePipeline(app);
    MapEndpoints(app);
    app.Run();
}
```

Cada bloque de configuración va en un método privado con nombre.

## Responsabilidades por capa

- **Controllers**: delgados. Solo traducen HTTP ↔ command/query y no hacen nada más. Las reglas viven en Application y Domain.
- **Infrastructure `*Module.cs`**: solo registran dependencias. La configuración EF, los repositorios y los reads van cada uno en su archivo.
