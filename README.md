# Backend Deployment

For local MySQL setup and run commands, see [LOCAL_SETUP.md](LOCAL_SETUP.md).

Deploy this folder as a Docker web service.

## Render Settings

```txt
Root Directory: backend
Runtime: Docker
Dockerfile Path: ./Dockerfile
Health Check Path: /health
```

## Required Environment Variables

```txt
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ExposeOtpForDevelopment=false
MYSQL_ADDON_URI=mysql://USER:PASSWORD@HOST:3306/DATABASE
Jwt__Issuer=FlipShop
Jwt__Audience=FlipShop.Angular
Jwt__Key=CHANGE_THIS_TO_A_LONG_RANDOM_SECRET
Cors__AllowedOrigins__0=https://YOUR_VERCEL_APP.vercel.app
Smtp__Host=YOUR_SMTP_HOST
Smtp__Port=587
Smtp__EnableSsl=true
Smtp__Username=YOUR_SMTP_USERNAME
Smtp__Password=YOUR_SMTP_PASSWORD
Smtp__From=admin@yourdomain.com
Smtp__AdminName=Bestic Fashion
```

You can also use `ConnectionStrings__DefaultConnection=Server=HOST;Port=3306;Database=DATABASE;User=USER;Password=PASSWORD;`.

After deploy, test `/health` and `/swagger`.

## Railway Settings

Set the service root to this backend folder and deploy with the Dockerfile.

Healthcheck path:

```txt
/health
```

Required Railway service variables:

```txt
ASPNETCORE_ENVIRONMENT=Production
Jwt__Issuer=FlipShop
Jwt__Audience=FlipShop.Angular
Jwt__Key=CHANGE_THIS_TO_A_LONG_RANDOM_SECRET_AT_LEAST_32_CHARS
ConnectionStrings__DefaultConnection=Server=HOST;Port=3306;Database=DATABASE;User=USER;Password=PASSWORD;SslMode=Required;
Cors__AllowedOrigins=https://YOUR_FRONTEND_DOMAIN
ExposeOtpForDevelopment=false
```

Use one database source. `ConnectionStrings__DefaultConnection` is preferred, but Railway MySQL variables also work:

```txt
MYSQL_ADDON_URI=mysql://USER:PASSWORD@HOST:3306/DATABASE
MYSQL_URL=mysql://USER:PASSWORD@HOST:3306/DATABASE
DATABASE_URL=mysql://USER:PASSWORD@HOST:3306/DATABASE
```

The backend also accepts Railway's split MySQL variables: `MYSQLHOST`, `MYSQLPORT`, `MYSQLDATABASE`, `MYSQLUSER`, and `MYSQLPASSWORD`.

Optional:

```txt
MYSQL_SERVER_VERSION=8.0.36
```

If the backend logs show `Production configuration is incomplete`, Railway is missing one of `Jwt__Key`, `ConnectionStrings__DefaultConnection`/`MYSQL_ADDON_URI`/`MYSQL_URL`, or `Cors__AllowedOrigins`.
