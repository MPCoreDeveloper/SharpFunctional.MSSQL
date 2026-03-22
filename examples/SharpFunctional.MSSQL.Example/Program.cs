// ---------------------------------------------------------------------------
// SharpFunctional.MSSQL — Example Console Application
// ---------------------------------------------------------------------------
// Demonstrates functional-first SQL Server access using EF Core, Dapper,
// transactions, and LanguageExt's Fin<T>/Option<T>/Seq<T> types.
//
// Requires: SQL Server LocalDB  →  (localdb)\MSSQLLocalDB
// The example database "SharpFunctionalExample" is created automatically.
// ---------------------------------------------------------------------------

using LanguageExt;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharpFunctional.MsSql;
using SharpFunctional.MsSql.Common;
using SharpFunctional.MsSql.DependencyInjection;
using SharpFunctional.MsSql.Example.Data;
using SharpFunctional.MsSql.Example.Models;
using SharpFunctional.MsSql.Transactions;
using static LanguageExt.Prelude;

const string connectionString =
    "Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=SharpFunctionalExample;" +
    "Integrated Security=True;Encrypt=True;TrustServerCertificate=False;Command Timeout=30";

Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║   SharpFunctional.MSSQL — Full Example      ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 1. Database setup — connect to LocalDB and recreate schema
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("▸ Setting up database...");

var options = new DbContextOptionsBuilder<SampleDbContext>()
    .UseSqlServer(connectionString)
    .Options;

await using var dbContext = new SampleDbContext(options);
await dbContext.Database.EnsureDeletedAsync();
await dbContext.Database.EnsureCreatedAsync();
Console.WriteLine("  ✓ Database 'SharpFunctionalExample' created on (localdb)\\MSSQLLocalDB");
Console.WriteLine();

// Create functional facades: EF-based and Dapper-based
await using var sqlConnection = new SqlConnection(connectionString);
await sqlConnection.OpenAsync();

var db = new FunctionalMsSqlDb(dbContext: dbContext, connection: sqlConnection);

// ═══════════════════════════════════════════════════════════════════════════
// 2. Seed data — customers, products, orders, and order lines
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("▸ Seeding customers...");

Customer[] customers =
[
    new() { FirstName = "Alice",  LastName = "Jansen",  Email = "alice@example.com" },
    new() { FirstName = "Bob",    LastName = "De Vries", Email = "bob@example.com" },
    new() { FirstName = "Claire", LastName = "Bakker",  Email = "claire@example.com" },
];

foreach (var customer in customers)
{
    var result = await db.Ef().AddAsync(customer);
    result.IfSucc(_ => Console.WriteLine($"  ✓ {customer.FirstName} {customer.LastName}"));
    result.IfFail(err => Console.WriteLine($"  ✗ Failed: {err}"));
}

await dbContext.SaveChangesAsync();
Console.WriteLine();

Console.WriteLine("▸ Seeding products...");

Product[] products =
[
    new() { Name = "Mechanical Keyboard", Category = "Peripherals", Price = 129.99m, Stock = 150 },
    new() { Name = "Gaming Mouse",        Category = "Peripherals", Price = 69.99m,  Stock = 300 },
    new() { Name = "4K Monitor",          Category = "Displays",    Price = 549.00m, Stock = 45 },
    new() { Name = "USB-C Hub",           Category = "Peripherals", Price = 39.99m,  Stock = 200 },
    new() { Name = "Webcam HD",           Category = "Peripherals", Price = 89.99m,  Stock = 80 },
    new() { Name = "Standing Desk",       Category = "Furniture",   Price = 799.00m, Stock = 20 },
    new() { Name = "Desk Lamp",           Category = "Furniture",   Price = 49.99m,  Stock = 120 },
    new() { Name = "Noise-Cancelling Headset", Category = "Audio",  Price = 249.00m, Stock = 60 },
];

foreach (var product in products)
{
    var result = await db.Ef().AddAsync(product);
    result.IfSucc(_ => Console.WriteLine($"  ✓ {product.Name}"));
    result.IfFail(err => Console.WriteLine($"  ✗ Failed: {err}"));
}

await dbContext.SaveChangesAsync();
Console.WriteLine();

Console.WriteLine("▸ Seeding orders with lines...");

// Alice orders keyboard + monitor
var order1 = new Order { CustomerId = customers[0].Id, Status = "Shipped" };
order1.Lines.Add(new OrderLine { ProductId = products[0].Id, Quantity = 1, UnitPrice = products[0].Price });
order1.Lines.Add(new OrderLine { ProductId = products[2].Id, Quantity = 2, UnitPrice = products[2].Price });

// Bob orders mouse + USB-C hub + headset
var order2 = new Order { CustomerId = customers[1].Id, Status = "Processing" };
order2.Lines.Add(new OrderLine { ProductId = products[1].Id, Quantity = 2, UnitPrice = products[1].Price });
order2.Lines.Add(new OrderLine { ProductId = products[3].Id, Quantity = 1, UnitPrice = products[3].Price });
order2.Lines.Add(new OrderLine { ProductId = products[7].Id, Quantity = 1, UnitPrice = products[7].Price });

// Claire orders standing desk + desk lamp
var order3 = new Order { CustomerId = customers[2].Id, Status = "Pending" };
order3.Lines.Add(new OrderLine { ProductId = products[5].Id, Quantity = 1, UnitPrice = products[5].Price });
order3.Lines.Add(new OrderLine { ProductId = products[6].Id, Quantity = 3, UnitPrice = products[6].Price });

// Alice places a second order: webcam
var order4 = new Order { CustomerId = customers[0].Id, Status = "Pending" };
order4.Lines.Add(new OrderLine { ProductId = products[4].Id, Quantity = 1, UnitPrice = products[4].Price });

foreach (var order in (Order[])[order1, order2, order3, order4])
{
    var result = await db.Ef().AddAsync(order);
    result.IfSucc(_ => Console.WriteLine($"  ✓ Order for customer #{order.CustomerId} ({order.Lines.Count} lines)"));
}

await dbContext.SaveChangesAsync();
Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 3. EF queries — GetByIdAsync, FindOneAsync, QueryAsync
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── EF Core Queries ──────────────────────────────");
Console.WriteLine();

Console.WriteLine("▸ Find customer by ID (1):");
var foundCustomer = await db.Ef().GetByIdAsync<Customer, int>(1);
foundCustomer.IfSome(c => Console.WriteLine($"  Found: {c}"));
foundCustomer.IfNone(() => Console.WriteLine("  Not found."));
Console.WriteLine();

Console.WriteLine("▸ Find product by name 'Gaming Mouse':");
var mouse = await db.Ef().FindOneAsync<Product>(p => p.Name == "Gaming Mouse");
mouse.IfSome(p => Console.WriteLine($"  Found: {p}"));
mouse.IfNone(() => Console.WriteLine("  Not found."));
Console.WriteLine();

Console.WriteLine("▸ Query all peripherals:");
var peripherals = await db.Ef().QueryAsync<Product>(p => p.Category == "Peripherals");
foreach (var p in peripherals)
{
    Console.WriteLine($"  - {p}");
}

Console.WriteLine();

Console.WriteLine("▸ Query all pending orders:");
var pendingOrders = await db.Ef().QueryAsync<Order>(o => o.Status == "Pending");
foreach (var o in pendingOrders)
{
    Console.WriteLine($"  - {o}");
}

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 4. Aggregates — CountAsync, AnyAsync
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Aggregates ──────────────────────────────────");
Console.WriteLine();

Console.WriteLine("▸ Count products priced over €100:");
var expensiveCount = await db.Ef().CountAsync<Product>(p => p.Price > 100m);
expensiveCount.IfSucc(c => Console.WriteLine($"  Count: {c}"));
expensiveCount.IfFail(err => Console.WriteLine($"  Error: {err}"));

Console.WriteLine();

Console.WriteLine("▸ Any customer named 'Bob'?");
var anyBob = await db.Ef().AnyAsync<Customer>(c => c.FirstName == "Bob");
anyBob.IfSucc(any => Console.WriteLine($"  Result: {any}"));

Console.WriteLine();

Console.WriteLine("▸ Any order with status 'Cancelled'?");
var anyCancelled = await db.Ef().AnyAsync<Order>(o => o.Status == "Cancelled");
anyCancelled.IfSucc(any => Console.WriteLine($"  Result: {any}"));

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 5. Functional chaining — Option → Seq, Option → Option, Seq → Seq
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Functional Chaining ─────────────────────────");
Console.WriteLine();

Console.WriteLine("▸ Find customer by ID → query their orders:");
var customerOrders = await db.Ef()
    .GetByIdAsync<Customer, int>(1)
    .Bind(c => db.Ef().QueryAsync<Order>(o => o.CustomerId == c.Id));

Console.WriteLine($"  Customer #1 has {customerOrders.Count} order(s):");
foreach (var o in customerOrders)
{
    Console.WriteLine($"    - {o}");
}

Console.WriteLine();

Console.WriteLine("▸ Find product by ID → check if any order lines exist:");
var hasOrders = await db.Ef()
    .GetByIdAsync<Product, int>(products[0].Id)
    .Bind(p => db.Ef().FindOneAsync<OrderLine>(l => l.ProductId == p.Id));

hasOrders.IfSome(l => Console.WriteLine($"  Product '{products[0].Name}' has been ordered (line #{l.Id})."));
hasOrders.IfNone(() => Console.WriteLine($"  Product '{products[0].Name}' has no orders."));

Console.WriteLine();

Console.WriteLine("▸ Query peripherals → map to summary strings:");
var summaries = await db.Ef()
    .QueryAsync<Product>(p => p.Category == "Peripherals")
    .Map(seq => seq.Map(p => $"{p.Name}: €{p.Price:F2} ({p.Stock} in stock)"));

foreach (var s in summaries)
{
    Console.WriteLine($"  - {s}");
}

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 6. Dapper queries — raw SQL against the same database
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Dapper Queries ──────────────────────────────");
Console.WriteLine();

Console.WriteLine("▸ Dapper: total revenue per product (raw SQL):");
var revenue = await db.Dapper().QueryAsync<ProductRevenue>(
    """
    SELECT p.Name AS ProductName, ISNULL(SUM(ol.Quantity * ol.UnitPrice), 0) AS TotalRevenue
    FROM Products p
    LEFT JOIN OrderLines ol ON ol.ProductId = p.Id
    GROUP BY p.Name
    HAVING ISNULL(SUM(ol.Quantity * ol.UnitPrice), 0) > 0
    ORDER BY TotalRevenue DESC
    """,
    new { });

foreach (var r in revenue)
{
    Console.WriteLine($"  {r.ProductName}: €{r.TotalRevenue:F2}");
}

Console.WriteLine();

Console.WriteLine("▸ Dapper: find single customer by email:");
var byEmail = await db.Dapper().QuerySingleAsync<CustomerDto>(
    "SELECT Id, FirstName, LastName, Email FROM Customers WHERE Email = @Email",
    new { Email = "bob@example.com" });

byEmail.IfSome(c => Console.WriteLine($"  Found: [{c.Id}] {c.FirstName} {c.LastName} ({c.Email})"));
byEmail.IfNone(() => Console.WriteLine("  Not found."));

Console.WriteLine();

Console.WriteLine("▸ Dapper: order summary with customer info:");
var orderSummaries = await db.Dapper().QueryAsync<OrderSummary>(
    """
    SELECT o.Id AS OrderId, c.FirstName + ' ' + c.LastName AS CustomerName, 
           o.Status, o.OrderDate,
           COUNT(ol.Id) AS LineCount, SUM(ol.Quantity * ol.UnitPrice) AS OrderTotal
    FROM Orders o
    INNER JOIN Customers c ON c.Id = o.CustomerId
    INNER JOIN OrderLines ol ON ol.OrderId = o.Id
    GROUP BY o.Id, c.FirstName, c.LastName, o.Status, o.OrderDate
    ORDER BY o.Id
    """,
    new { });

foreach (var os in orderSummaries)
{
    Console.WriteLine($"  Order #{os.OrderId} | {os.CustomerName} | {os.Status} | {os.LineCount} items | €{os.OrderTotal:F2}");
}

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 7. Transactions — update within a transaction, rollback on failure
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Transactions ────────────────────────────────");
Console.WriteLine();

Console.WriteLine("▸ Transaction: apply 10% discount to all 'Displays' products...");
var discountResult = await db.InTransactionAsync(async txDb =>
{
    var ef = txDb.Ef().WithTracking();
    var displays = await ef.QueryAsync<Product>(p => p.Category == "Displays");

    foreach (var display in displays)
    {
        display.Price *= 0.90m;
        var saveResult = await ef.SaveAsync(display);
        if (saveResult.IsFail) return FinFail<int>(LanguageExt.Common.Error.New("Save failed during discount"));
    }

    return Fin<int>.Succ(displays.Count);
});

discountResult.IfSucc(count => Console.WriteLine($"  ✓ Discounted {count} product(s)"));
discountResult.IfFail(err => Console.WriteLine($"  ✗ Transaction failed: {err}"));

Console.WriteLine();

Console.WriteLine("▸ Transaction: ship order #2 (Bob's order)...");
var shipResult = await db.InTransactionAsync(async txDb =>
{
    var ef = txDb.Ef().WithTracking();
    var order = await ef.FindOneAsync<Order>(o => o.Id == order2.Id);

    return await order.Match(
        Some: async o =>
        {
            o.Status = "Shipped";
            return await ef.SaveAsync(o);
        },
        None: () => Task.FromResult(FinFail<LanguageExt.Unit>(
            LanguageExt.Common.Error.New("Order not found"))));
});

shipResult.IfSucc(_ => Console.WriteLine("  ✓ Order #2 status updated to 'Shipped'"));
shipResult.IfFail(err => Console.WriteLine($"  ✗ Transaction failed: {err}"));

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 8. TransactionMap — transform result inside a transaction
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── TransactionMap ──────────────────────────────");
Console.WriteLine();

Console.WriteLine("▸ TransactionMap: count in-stock products inside a transaction...");
var countMsg = await db.InTransactionMapAsync(
    async txDb =>
    {
        var ef = txDb.Ef();
        return await ef.CountAsync<Product>(p => p.Stock > 0);
    },
    count => $"Total in-stock products: {count}");

countMsg.IfSucc(msg => Console.WriteLine($"  ✓ {msg}"));
countMsg.IfFail(err => Console.WriteLine($"  ✗ Error: {err}"));

Console.WriteLine();

Console.WriteLine("▸ TransactionMap: compute total order value for customer Alice...");
var aliceTotal = await db.InTransactionMapAsync(
    async txDb =>
    {
        var lines = await txDb.Ef().QueryAsync<OrderLine>(l => l.Order!.CustomerId == customers[0].Id);
        var total = lines.Sum(l => l.Quantity * l.UnitPrice);
        return Fin<decimal>.Succ(total);
    },
    total => $"Alice's total spend: €{total:F2}");

aliceTotal.IfSucc(msg => Console.WriteLine($"  ✓ {msg}"));
aliceTotal.IfFail(err => Console.WriteLine($"  ✗ Error: {err}"));

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 9. Delete — remove an entity and verify
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Delete ──────────────────────────────────────");
Console.WriteLine();

Console.WriteLine($"▸ Delete product 'Desk Lamp' (ID={products[6].Id}):");
var deleteResult = await db.Ef().DeleteByIdAsync<Product, int>(products[6].Id);
deleteResult.IfSucc(_ => Console.WriteLine("  ✓ Deleted."));
deleteResult.IfFail(err => Console.WriteLine($"  ✗ Error: {err}"));

var afterDelete = await db.Ef().GetByIdAsync<Product, int>(products[6].Id);
afterDelete.IfNone(() => Console.WriteLine("  Confirmed: 'Desk Lamp' no longer exists."));
afterDelete.IfSome(_ => Console.WriteLine("  ✗ Unexpected: product still exists!"));

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 10. Final summary via Dapper
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Final Summary (Dapper) ──────────────────────");
Console.WriteLine();

var stats = await db.Dapper().QuerySingleAsync<DbStats>(
    """
    SELECT 
        (SELECT COUNT(*) FROM Customers) AS CustomerCount,
        (SELECT COUNT(*) FROM Products) AS ProductCount,
        (SELECT COUNT(*) FROM Orders) AS OrderCount,
        (SELECT COUNT(*) FROM OrderLines) AS OrderLineCount
    """,
    new { });

stats.IfSome(s => Console.WriteLine(
    $"  Customers: {s.CustomerCount} | Products: {s.ProductCount} | Orders: {s.OrderCount} | Lines: {s.OrderLineCount}"));

Console.WriteLine();

// ═══════════════════════════════════════════════════════════════════════════
// 11. Dependency Injection — register and resolve via IServiceCollection
// ═══════════════════════════════════════════════════════════════════════════

Console.WriteLine("── Dependency Injection ────────────────────────");
Console.WriteLine();

Console.WriteLine("▸ Building DI container with EF Core + Dapper backends...");

var diServices = new ServiceCollection();

diServices.AddDbContext<SampleDbContext>(o => o.UseSqlServer(connectionString));

diServices.AddFunctionalMsSql<SampleDbContext>(opts =>
{
    opts.ConnectionString = connectionString;
    opts.ExecutionOptions = new SqlExecutionOptions(commandTimeoutSeconds: 30, maxRetryCount: 2);
});

await using var diProvider = diServices.BuildServiceProvider();
Console.WriteLine("  ✓ Container built");
Console.WriteLine();

Console.WriteLine("▸ Resolving FunctionalMsSqlDb from a DI scope...");

await using var diScope = diProvider.CreateAsyncScope();
var diDb = diScope.ServiceProvider.GetRequiredService<FunctionalMsSqlDb>();

Console.WriteLine("  ✓ FunctionalMsSqlDb resolved from scope");
Console.WriteLine();

Console.WriteLine("▸ EF query via DI-resolved facade (customers):");
var diCustomers = await diDb.Ef().QueryAsync<Customer>(c => c.Id > 0);
foreach (var c in diCustomers)
{
    Console.WriteLine($"  [{c.Id}] {c.FirstName} {c.LastName} <{c.Email}>");
}

Console.WriteLine();

Console.WriteLine("▸ Dapper query via DI-resolved facade (DB stats):");
var diStats = await diDb.Dapper().QuerySingleAsync<DbStats>(
    """
    SELECT
        (SELECT COUNT(*) FROM Customers) AS CustomerCount,
        (SELECT COUNT(*) FROM Products)  AS ProductCount,
        (SELECT COUNT(*) FROM Orders)    AS OrderCount,
        (SELECT COUNT(*) FROM OrderLines) AS OrderLineCount
    """,
    new { });

diStats.IfSome(s => Console.WriteLine(
    $"  Via DI: {s.CustomerCount} customers | {s.ProductCount} products | {s.OrderCount} orders | {s.OrderLineCount} lines"));

Console.WriteLine();

Console.WriteLine("╔══════════════════════════════════════════════╗");
Console.WriteLine("║                    Done!                     ║");
Console.WriteLine("╚══════════════════════════════════════════════╝");

// ═══════════════════════════════════════════════════════════════════════════
// DTOs for Dapper queries
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>Revenue per product projection for Dapper.</summary>
internal sealed record ProductRevenue(string ProductName, decimal TotalRevenue);

/// <summary>Customer DTO for Dapper queries.</summary>
internal sealed record CustomerDto(int Id, string FirstName, string LastName, string Email);

/// <summary>Order summary projection for Dapper.</summary>
internal sealed record OrderSummary(int OrderId, string CustomerName, string Status, DateTime OrderDate, int LineCount, decimal OrderTotal);

/// <summary>Database statistics projection.</summary>
internal sealed record DbStats(int CustomerCount, int ProductCount, int OrderCount, int OrderLineCount);
