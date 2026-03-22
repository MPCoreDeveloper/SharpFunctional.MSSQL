// ---------------------------------------------------------------------------
// SharpFunctional.MSSQL — Dependency Injection Example
// ---------------------------------------------------------------------------
// Demonstrates registering FunctionalMsSqlDb in IServiceCollection and
// consuming it via constructor injection in a service class.
//
// Shows all three registration overloads:
//   • AddFunctionalMsSqlEf<TContext>()    — EF Core only
//   • AddFunctionalMsSqlDapper()          — Dapper only
//   • AddFunctionalMsSql<TContext>()      — EF Core + Dapper combined
//
// Requires: SQL Server LocalDB  →  (localdb)\MSSQLLocalDB
// The example database "SharpFunctionalDiExample" is created automatically.
// ---------------------------------------------------------------------------

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpFunctional.MsSql;
using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.DependencyInjection;
using SharpFunctional.MsSql.DiExample.Data;
using SharpFunctional.MsSql.DiExample.Models;
using SharpFunctional.MsSql.DiExample.Services;
using SharpFunctional.MsSql.Ef;

const string connectionString =
    "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=SharpFunctionalDiExample;" +
    "Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Command Timeout=30";

Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║  SharpFunctional.MSSQL — DI Example         ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// Database setup
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("▸ Creating database schema...");
var setupOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
await using (var setup = new AppDbContext(setupOptions))
{
    await setup.Database.EnsureDeletedAsync();
    await setup.Database.EnsureCreatedAsync();
}

Console.WriteLine("  ✓ Database 'SharpFunctionalDiExample' ready");
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// Registration overload 1 — EF Core only
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Registration: EF Core only ──────────────────");
Console.WriteLine();

var efServices = new ServiceCollection();

efServices.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
efServices.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

efServices.AddFunctionalMsSqlEf<AppDbContext>(opts =>
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 30));

efServices.AddScoped<ProductService>();

await using var efProvider = efServices.BuildServiceProvider();

await using (var scope = efProvider.CreateAsyncScope())
{
    var svc = scope.ServiceProvider.GetRequiredService<ProductService>();

    Console.WriteLine("▸ Seeding products via EF...");

    Product[] products =
    [
        new() { Name = "Mechanical Keyboard", Category = "Peripherals", Price = 129.99m, Stock = 50 },
        new() { Name = "Gaming Mouse",        Category = "Peripherals", Price = 69.99m,  Stock = 120 },
        new() { Name = "4K Monitor",          Category = "Displays",    Price = 549.00m, Stock = 15 },
        new() { Name = "USB-C Hub",           Category = "Peripherals", Price = 39.99m,  Stock = 80 },
        new() { Name = "Standing Desk",       Category = "Furniture",   Price = 799.00m, Stock = 8 },
    ];

    foreach (var p in products)
    {
        var result = await svc.AddProductAsync(p);
        result.IfSucc(saved => Console.WriteLine($"  ✓ {saved.Name} (ID={saved.Id})"));
        result.IfFail(err  => Console.WriteLine($"  ✗ {p.Name}: {err}"));
    }

    Console.WriteLine();
    Console.WriteLine("▸ EF query — all peripherals via ProductService:");
    var peripherals = await svc.GetByCategoryAsync("Peripherals");
    foreach (var p in peripherals)
        Console.WriteLine($"  {p}");

    Console.WriteLine();
    Console.WriteLine("▸ EF count — products in stock:");
    var count = await svc.CountInStockAsync();
    count.IfSucc(n  => Console.WriteLine($"  In stock: {n} product(s)"));
    count.IfFail(err => Console.WriteLine($"  Error: {err}"));

    Console.WriteLine();
    Console.WriteLine("▸ Paginated query — peripherals page 1 (2 per page):");
    var page = await svc.GetPaginatedAsync("Peripherals", pageNumber: 1, pageSize: 2);
    page.IfSucc(results =>
    {
        Console.WriteLine($"  Page {results.PageNumber}/{results.TotalPages} (total: {results.TotalCount})");
        foreach (var p in results.Items)
            Console.WriteLine($"    {p}");
    });

    Console.WriteLine();
    Console.WriteLine("▸ Specification query — products > €100 sorted by name:");
    var spec = new QuerySpecification<Product>(p => p.Price > 100m)
        .SetOrderBy(p => (object)p.Name);
    var specResults = await svc.GetBySpecificationAsync(spec);
    specResults.IfSome(list =>
    {
        Console.WriteLine($"  Found {list.Count} product(s):");
        foreach (var p in list)
            Console.WriteLine($"    {p}");
    });

    Console.WriteLine();
    Console.WriteLine("▸ Batch insert — 3 audio products:");
    Product[] audioProducts =
    [
        new() { Name = "Bluetooth Speaker", Category = "Audio", Price = 59.99m, Stock = 40 },
        new() { Name = "DAC Amplifier",     Category = "Audio", Price = 149.99m, Stock = 15 },
        new() { Name = "Studio Monitors",   Category = "Audio", Price = 299.99m, Stock = 10 },
    ];
    var batchResult = await svc.BatchInsertAsync(audioProducts, batchSize: 2);
    batchResult.IfSucc(inserted => Console.WriteLine($"  ✓ Inserted {inserted} state entries"));
    batchResult.IfFail(err => Console.WriteLine($"  ✗ Error: {err}"));

    Console.WriteLine();
    Console.WriteLine("▸ Stream all products:");
    var streamCount = 0;
    await foreach (var p in svc.StreamAllAsync())
    {
        streamCount++;
        Console.WriteLine($"    [{streamCount}] {p}");
    }
    Console.WriteLine($"  ✓ Streamed {streamCount} product(s)");
}

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// Registration overload 2 — Dapper only
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Registration: Dapper only ───────────────────");
Console.WriteLine();

var dapperServices = new ServiceCollection();

dapperServices.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));

dapperServices.AddFunctionalMsSqlDapper(connectionString, opts =>
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 30, maxRetryCount: 3));

dapperServices.AddScoped<ProductService>();

await using var dapperProvider = dapperServices.BuildServiceProvider();

await using (var scope = dapperProvider.CreateAsyncScope())
{
    var svc = scope.ServiceProvider.GetRequiredService<ProductService>();

    Console.WriteLine("▸ Dapper query — product summaries via ProductService:");
    var summaries = await svc.GetSummariesAsync();
    foreach (var s in summaries)
        Console.WriteLine($"  {s.Name} ({s.Category}) — €{s.Price:F2}");
}

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// Registration overload 3 — EF Core + Dapper combined
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Registration: EF Core + Dapper combined ─────");
Console.WriteLine();

var combinedServices = new ServiceCollection();

combinedServices.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
combinedServices.AddDbContext<AppDbContext>(o => o.UseSqlServer(connectionString));

combinedServices.AddFunctionalMsSql<AppDbContext>(opts =>
{
    opts.ConnectionString = connectionString;
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 30, maxRetryCount: 2);
});

combinedServices.AddScoped<ProductService>();

await using var combinedProvider = combinedServices.BuildServiceProvider();

await using (var scope = combinedProvider.CreateAsyncScope())
{
    var svc = scope.ServiceProvider.GetRequiredService<ProductService>();

    Console.WriteLine("▸ EF Core — find product by ID:");
    var product = await svc.GetByIdAsync(1);
    product.IfSome(p  => Console.WriteLine($"  Found: {p}"));
    product.IfNone(()  => Console.WriteLine("  Not found."));

    Console.WriteLine();
    Console.WriteLine("▸ Dapper — product summaries:");
    var summaries = await svc.GetSummariesAsync();
    foreach (var s in summaries)
        Console.WriteLine($"  {s.Name} ({s.Category}) — €{s.Price:F2}");

    Console.WriteLine();
    Console.WriteLine("▸ Circuit breaker — wrap a query with resilience:");
    var breaker = new CircuitBreaker(new CircuitBreakerOptions
    {
        FailureThreshold = 3,
        OpenDuration = TimeSpan.FromSeconds(10),
        SuccessThresholdInHalfOpen = 1,
    });

    var cbResult = await breaker.ExecuteAsync(async ct =>
        await svc.CountInStockAsync(ct));

    cbResult.IfSucc(n => Console.WriteLine($"  ✓ In stock: {n} (circuit: {breaker.State})"));
    cbResult.IfFail(err => Console.WriteLine($"  ✗ Error: {err}"));
}

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║                    Done!                     ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
