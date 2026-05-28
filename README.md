# Backend Deployment

Deploy this folder to Render as a Docker web service.

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
UseInMemoryDatabase=false
ExposeOtpForDevelopment=false
ConnectionStrings__DefaultConnection=Server=YOUR_CLEVER_HOST;Port=3306;Database=YOUR_DB;User=YOUR_USER;Password=YOUR_PASSWORD;
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

After deploy, test `/health` and `/swagger`.
