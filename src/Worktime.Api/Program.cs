using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Worktime.Api.Auth;
using Worktime.Api.Middleware;
using Worktime.Api.Realtime;
using Worktime.Application;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Infrastructure;
using Worktime.Infrastructure.Redis;

namespace Worktime.Api;

public class Program // not static: WebApplicationFactory<Program> needs it as a type argument
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Logging.ClearProviders().AddJsonConsole(o => { o.IncludeScopes = true; o.TimestampFormat = "O"; });

        AddModules(builder.Services, builder.Configuration);
        AddAuth(builder.Services, builder.Configuration);
        AddRateLimiting(builder.Services);
        AddWeb(builder.Services, builder.Configuration);

        var app = builder.Build();
        UsePipeline(app);
        MapEndpoints(app);
        app.Run();
    }

    /// <summary>Each module owns its DI registrations.</summary>
    private static void AddModules(IServiceCollection services, IConfiguration config) => services
        .AddApplication()
        .AddInfrastructure(config)
        .AddMediator(o =>
        {
            o.ServiceLifetime = ServiceLifetime.Scoped; // handlers depend on the scoped DbContext
            o.PipelineBehaviors = [typeof(LoggingBehavior<,>), typeof(ValidationBehavior<,>)];
        });

    private static void AddAuth(IServiceCollection services, IConfiguration config)
    {
        services.Configure<JwtOptions>(config.GetSection(JwtOptions.Section));
        var jwt = config.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        if (jwt.SigningKey.Length < 32) throw new InvalidOperationException("Jwt:SigningKey must be at least 32 characters.");

        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddAuthorization();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
        {
            o.MapInboundClaims = false; // keep "sub", "role", "perm" as issued
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,
                IssuerSigningKey = jwt.Key,
                NameClaimType = WorktimeClaims.Name,
                RoleClaimType = WorktimeClaims.Role,
                ClockSkew = TimeSpan.FromSeconds(30),
            };
            o.Events = new JwtBearerEvents
            {
                // Browsers cannot set headers on WebSockets: SignalR sends the token in the query string, for the hub only.
                OnMessageReceived = ctx =>
                {
                    if (ctx.HttpContext.Request.Path.StartsWithSegments(WorktimeHub.Path) && ctx.Request.Query["access_token"] is { Count: > 0 } token)
                        ctx.Token = token;
                    return Task.CompletedTask;
                },
                // Revocation: a token whose stamp is no longer current (reset, deactivation, permission change) is dead.
                OnTokenValidated = async ctx =>
                {
                    var stamps = ctx.HttpContext.RequestServices.GetRequiredService<StampValidator>();
                    var principal = ctx.Principal!;
                    if (!await stamps.IsCurrentAsync(principal.UserId(), principal.FindFirst(WorktimeClaims.Stamp)?.Value ?? "", ctx.HttpContext.RequestAborted))
                        ctx.Fail("auth.stale_session");
                },
            };
        });
    }

    private static void AddRateLimiting(IServiceCollection services) => services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.AddPolicy(RateLimits.Login, ctx => RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    });

    private static void AddWeb(IServiceCollection services, IConfiguration config)
    {
        services.Configure<DiagnosticsOptions>(config.GetSection(DiagnosticsOptions.Section));
        services.AddProblemDetails();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        services.AddSignalR(o => o.AddFilter<HubGuardFilter>())
            .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .AddStackExchangeRedis(config.GetConnectionString("Redis")!, o => o.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("worktime"));
        services.AddSingleton<IRealtimeNotifier, SignalRNotifier>();
        services.AddCors(o => o.AddDefaultPolicy(p => p
            .WithOrigins(config.GetSection("Cors:Origins").Get<string[]>() ?? [])
            .AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownNetworks.Clear(); // nginx sits on the compose network
            o.KnownProxies.Clear();
        });
    }

    /// <summary>Order matters.</summary>
    private static void UsePipeline(WebApplication app)
    {
        app.UseForwardedHeaders();                       // real client IP for the rate limiter and logs
        app.UseExceptionHandler();                       // outermost: anything below becomes ProblemDetails
        app.UseMiddleware<CorrelationIdMiddleware>();    // id available to every later log line
        app.UseMiddleware<RequestTimingMiddleware>();    // times everything below, including auth
        app.UseCors();
        app.UseAuthentication();                         // who are you (validates JWT + stamp)
        app.UseRateLimiter();                            // after routing data exists, before endpoints
        app.UseAuthorization();                          // may you (perm:* policies)
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapControllers();
        app.MapHub<WorktimeHub>(WorktimeHub.Path);
        app.MapHealthChecks("/health");
    }
}
