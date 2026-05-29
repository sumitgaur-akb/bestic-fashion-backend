# Local Backend Setup

Use this when running the backend on another machine with a local MySQL server.

## Requirements

- .NET SDK 10
- MySQL running on `localhost:3306`
- Database user matching `src/FlipShop.Api/appsettings.Development.json`

Current development connection string:

```txt
Server=localhost;Port=3306;Database=flipshop_dev;User=root;Password=root;
```

## MySQL

Create the database if it does not exist:

```sql
CREATE DATABASE IF NOT EXISTS flipshop_dev;
```

Make sure the configured user can connect:

```sql
SELECT 1;
```

The API calls `EnsureCreatedAsync()` on startup, so it creates the tables automatically when the database connection works.

## Run

From this backend folder:

```powershell
dotnet restore .\FlipShop.slnx
dotnet run --project .\src\FlipShop.Api\FlipShop.Api.csproj --urls http://127.0.0.1:5000
```

Or use:

```powershell
.\scripts\run-local.ps1
```

## Gmail SMTP

SMTP is configured for:

```txt
besticfashion.myntra@gmail.com
smtp.gmail.com:587
```

The Gmail SMTP password is already configured in appsettings for this project. If you need to override it on another machine or deployment, set this environment variable before running:

```powershell
$env:Smtp__Password = "YOUR_16_CHARACTER_GOOGLE_APP_PASSWORD"
.\scripts\run-local.ps1
```

For a persistent Windows user-level value:

```powershell
[Environment]::SetEnvironmentVariable("Smtp__Password", "YOUR_16_CHARACTER_GOOGLE_APP_PASSWORD", "User")
```

Open a new terminal after setting the persistent value.

If build fails with a message like `The process cannot access the file ... because it is being used by another process`, stop the old backend process first:

```powershell
.\scripts\stop-local.ps1
dotnet build .\FlipShop.slnx
```

## Test

```powershell
Invoke-RestMethod http://127.0.0.1:5000/health
Invoke-RestMethod http://127.0.0.1:5000/api/products
```

Seed login:

```txt
customer@flipshop.local / Password123!
seller@flipshop.local / Password123!
admin@flipshop.local / Password123!
```

## Common Startup Problems

- MySQL is not running on `localhost:3306`.
- Database `flipshop_dev` does not exist.
- MySQL user/password is not `root/root`.
- App is running as `Production`, so it reads `appsettings.Production.json` instead of `appsettings.Development.json`.
- .NET SDK 10 is not installed.
