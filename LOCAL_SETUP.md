# Local Backend Setup

Use this when running the backend on another machine with a local MySQL server.

## Requirements

- .NET SDK 10
- MySQL running on `localhost:3306`
- Database user matching `src/FlipShop.Api/appsettings.Development.json`

Current development connection string:

```txt
Server=localhost;Port=3306;Database=flipshop;User=root;Password=root;SslMode=None;
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

Run the SQL scripts in the database folder before starting the API. The API connects to the configured MySQL database and does not create or reset tables on startup.

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

## SMTP

SMTP is disabled by default for local development. To send real email, configure these environment variables before running:

```powershell
$env:Smtp__Host = "smtp.gmail.com"
$env:Smtp__Port = "587"
$env:Smtp__EnableSsl = "true"
$env:Smtp__Username = "YOUR_SMTP_USERNAME"
$env:Smtp__Password = "YOUR_SMTP_PASSWORD"
$env:Smtp__From = "YOUR_FROM_ADDRESS"
.\scripts\run-local.ps1
```

Never commit SMTP passwords or database credentials to appsettings files.

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
