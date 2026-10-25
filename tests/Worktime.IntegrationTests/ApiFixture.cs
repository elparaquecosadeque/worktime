using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;
using Worktime.Api;
using Worktime.Application.Common.Interfaces;
using Worktime.Infrastructure.Seeding;

namespace Worktime.IntegrationTests;

/// <summary>
/// Real Postgres + Redis in containers, the real API in-process, demo data seeded once (tenants "demo" and "acme").
/// The Host picks the tenant: clients talk to http://{slug}.localhost, the root host is http://localhost.
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
    private readonly RedisContainer _redis = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _redis.StartAsync());
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString())
            .UseSetting("ConnectionStrings:Redis", _redis.GetConnectionString())
            .UseSetting("Seed:Enabled", "true")
            .UseSetting("Seed:ResetInterval", "365.00:00:00"));
        _ = Factory.Server; // boots the host: migrations + seed
    }

    public HttpClient Client(string? slug = "demo") =>
        Factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri(slug is null ? "http://localhost" : $"http://{slug}.localhost") });

    /// <summary>A DI scope working for one tenant, like a request on that tenant's host.</summary>
    public AsyncServiceScope TenantScope(Guid? tenantId = null)
    {
        var scope = Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Use(tenantId ?? DemoTenants.Demo);
        return scope;
    }

    public async Task<HttpClient> LoginAsync(string email, string slug = "demo")
    {
        var client = Client(slug);
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "Demo1234!" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _redis.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>;
