# SharpFunctional.MSSQL — Tests

## Prerequisites

All integration tests require a running SQL Server instance. The fixture automatically selects the appropriate connection method based on your platform:

- **Windows:** SQL Server LocalDB (via Integrated Security)
- **Linux/macOS:** Docker SQL Server container

### Windows Setup

#### SQL Server LocalDB

All integration tests require a running **SQL Server LocalDB** instance.  
LocalDB is installed automatically with Visual Studio (Data Storage and Processing workload) or can be installed separately via the [SQL Server Express LocalDB installer](https://learn.microsoft.com/sql/database-engine/configure-windows/sql-server-express-localdb).

**Verify LocalDB is running:**

```powershell
sqllocaldb info MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

If the instance does not exist, create it:

```powershell
sqllocaldb create MSSQLLocalDB
sqllocaldb start MSSQLLocalDB
```

### Linux/macOS Setup

#### Docker SQL Server

Use `docker-compose.yml` to spin up a SQL Server 2022 container:

```bash
cd tests
docker-compose up -d
```

This starts a container with:
- **Server:** localhost:1433
- **User:** sa
- **Password:** YourPassword123!

Wait for the container to be healthy (health check shows passing):

```bash
docker-compose ps
# Status should show "healthy"
```

To stop the container:

```bash
docker-compose down
```

### Connection Configuration

The `DatabaseFixture` reads these optional environment variables for advanced configuration:

| Variable | Default | Purpose |
|----------|---------|---------|
| `TEST_DB_SERVER` | `localhost` (Linux) or `(localdb)\MSSQLLocalDB` (Windows) | Database server/host |
| `TEST_DB_PORT` | `1433` (Linux) or N/A (LocalDB) | Database port |
| `TEST_DB_USER` | `sa` (Linux) or Integrated Security (Windows) | Username |
| `TEST_DB_PASSWORD` | `YourPassword123!` (Linux) | Password |

**Example: Override for custom Docker container:**

```bash
export TEST_DB_SERVER=my-custom-mssql-host
export TEST_DB_PORT=5433
export TEST_DB_USER=customuser
export TEST_DB_PASSWORD=CustomPassword123!
dotnet test tests/SharpFunctional.MSSQL.Tests
```

Or in PowerShell:

```powershell
$env:TEST_DB_SERVER = "my-custom-mssql-host"
$env:TEST_DB_PORT = "5433"
dotnet test tests/SharpFunctional.MSSQL.Tests
```

## Running Tests

```bash
# Standard run (uses platform-default connection)
dotnet test tests/SharpFunctional.MSSQL.Tests

# With verbose output
dotnet test tests/SharpFunctional.MSSQL.Tests --verbosity detailed

# Run specific test class
dotnet test tests/SharpFunctional.MSSQL.Tests --filter "FullyQualifiedName~FunctionalMsSqlDbTests"
```

Or use the Visual Studio Test Explorer.

## Connection Strings

### Windows (LocalDB)

```
Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=TestDB;Integrated Security=True;
Persist Security Info=False;Pooling=False;MultipleActiveResultSets=False;
Encrypt=True;TrustServerCertificate=False;Command Timeout=30
```

### Linux/macOS (Docker)

```
Server=localhost,1433;Database=TestDB;User Id=sa;Password=YourPassword123!;
Encrypt=True;TrustServerCertificate=True;Connection Timeout=30
```

**Note:** The `TestDB` database is **automatically created and recreated** by the test fixtures using EF Core `EnsureDeleted()` / `EnsureCreated()`. You do **not** need to create the database manually.

## Test Framework

Tests use **xUnit v3** (`xunit.v3` 3.2.2).

## Troubleshooting

### Windows: LocalDB connection fails

```
System.PlatformNotSupportedException: LocalDB is not supported on this platform.
```

- Ensure you're running on Windows and have SQL Server LocalDB installed.
- Run `sqllocaldb info MSSQLLocalDB` to verify the instance exists.
- Run `sqllocaldb start MSSQLLocalDB` to start the instance.

### Linux/macOS: Docker connection fails

```
System.Data.SqlClient.SqlException (0x80131904): Server not found
```

- Ensure Docker is running: `docker ps`
- Start the container: `docker-compose -f tests/docker-compose.yml up -d`
- Wait for health check to pass: `docker-compose -f tests/docker-compose.yml ps`
- Verify connectivity: `telnet localhost 1433` (or use `Test-NetConnection` on PowerShell)

### Container is starting but tests timeout

The SQL Server container takes 10–15 seconds to initialize. Retry the test run:

```bash
docker-compose -f tests/docker-compose.yml logs mssql
dotnet test tests/SharpFunctional.MSSQL.Tests --verbosity detailed
```

### Cannot connect to sa with password

Ensure the password matches what's in `docker-compose.yml`:

```yaml
SA_PASSWORD: YourPassword123!
```

If you changed it, update the `docker-compose.yml` and restart:

```bash
docker-compose -f tests/docker-compose.yml down
docker-compose -f tests/docker-compose.yml up -d
```

## CI/CD Integration

### GitHub Actions (Linux container)

In your workflow, add a service container or initialize Docker before running tests:

```yaml
services:
  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    env:
      ACCEPT_EULA: Y
      SA_PASSWORD: YourPassword123!
    options: >-
      --health-cmd="/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourPassword123! -Q 'SELECT 1' || exit 1"
      --health-interval 10s
      --health-timeout 5s
      --health-retries 5
    ports:
      - 1433:1433
```

Then run tests:

```yaml
- name: Run integration tests
  run: dotnet test tests/SharpFunctional.MSSQL.Tests
  env:
    TEST_DB_SERVER: mssql
