using System.Text;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using FlipShop.Api.Middleware;
using FlipShop.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

var generatedJwtKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

ValidateProductionConfiguration(builder.Configuration, builder.Environment);
var jwtKey = ResolveJwtKey(builder.Configuration, builder.Environment, generatedJwtKey);
builder.Configuration["Jwt:Key"] = jwtKey;

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var allowedOrigins = GetConfiguredOrigins(builder.Configuration);
        var allowPlatformPreviewOrigins = builder.Configuration.GetValue("Cors:AllowPlatformPreviewOrigins", builder.Environment.IsProduction());
        if (allowedOrigins.Contains("*"))
        {
            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException("Wildcard CORS is not allowed in production. Configure Cors:AllowedOrigins with explicit HTTP or HTTPS origins.");
            }

            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            return;
        }

        if (builder.Environment.IsProduction() && allowPlatformPreviewOrigins)
        {
            policy.SetIsOriginAllowed(IsAllowedPlatformPreviewOrigin).AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200").AllowAnyHeader().AllowAnyMethod();
    });
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("frontend");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", service = "FlipShop.Api" }));

app.Run();

static string[] GetConfiguredOrigins(IConfiguration configuration)
{
    var origins = new List<string>();
    origins.AddRange(configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []);
    origins.AddRange(SplitOrigins(configuration["Cors:AllowedOrigins"]));
    origins.AddRange(SplitOrigins(configuration["FRONTEND_ORIGIN"]));
    origins.AddRange(SplitOrigins(configuration["FRONTEND_ORIGINS"]));

    var distinctOrigins = origins
        .Select(origin => origin.Trim().TrimEnd('/'))
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    return distinctOrigins;
}

static IEnumerable<string> SplitOrigins(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static string ResolveJwtKey(IConfiguration configuration, IWebHostEnvironment environment, string generatedJwtKey)
{
    var configuredKey = configuration["Jwt:Key"];
    if (!string.IsNullOrWhiteSpace(configuredKey))
    {
        return configuredKey;
    }

    if (environment.IsProduction() && configuration.GetValue("Jwt:AllowGeneratedKey", true))
    {
        Console.WriteLine("WARNING: Jwt:Key is not configured. Using an ephemeral generated key for this process.");
        return generatedJwtKey;
    }

    throw new InvalidOperationException("Jwt:Key must be configured.");
}

static bool IsAllowedPlatformPreviewOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
    {
        return false;
    }

    return uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase)
        || uri.Host.EndsWith(".onrender.com", StringComparison.OrdinalIgnoreCase);
}

static void ValidateProductionConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
{
    if (!environment.IsProduction())
    {
        return;
    }

    var errors = new List<string>();
    var jwtKey = configuration["Jwt:Key"];
    var allowGeneratedJwtKey = configuration.GetValue("Jwt:AllowGeneratedKey", true);
    if (string.IsNullOrWhiteSpace(jwtKey))
    {
        if (!allowGeneratedJwtKey)
        {
            Require(jwtKey, "Jwt:Key", errors, minLength: 32);
        }
    }
    else if (jwtKey.Length < 32)
    {
        errors.Add("Jwt:Key must be configured with at least 32 characters.");
    }

    if (jwtKey?.Contains("replace-with", StringComparison.OrdinalIgnoreCase) == true)
    {
        errors.Add("Jwt:Key must not use the checked-in placeholder value.");
    }

    var defaultConnection = configuration.GetConnectionString("DefaultConnection");
    RequireAny(
        [
            defaultConnection,
            configuration["MYSQL_ADDON_URI"],
            configuration["MYSQL_URL"],
            configuration["DATABASE_URL"],
            configuration["MYSQL_ADDON_HOST"],
            configuration["MYSQLHOST"]
        ],
        "ConnectionStrings:DefaultConnection or MYSQL_ADDON_URI/MYSQL_URL",
        errors);
    var allowPlatformPreviewOrigins = configuration.GetValue("Cors:AllowPlatformPreviewOrigins", true);
    var origins = GetConfiguredOrigins(configuration);
    if (origins.Length == 0 && !allowPlatformPreviewOrigins)
    {
        errors.Add("Cors:AllowedOrigins must be configured when Cors:AllowPlatformPreviewOrigins is false.");
    }

    if (origins.Contains("*"))
    {
        errors.Add("Cors:AllowedOrigins cannot contain * in production.");
    }

    if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || !IsHttpOrHttps(uri)))
    {
        errors.Add("Cors:AllowedOrigins must contain only absolute HTTP or HTTPS origins in production.");
    }

    if (ContainsLocalOrPlaceholderDatabase(defaultConnection)
        || ContainsLocalOrPlaceholderDatabase(configuration["MYSQL_ADDON_URI"])
        || ContainsLocalOrPlaceholderDatabase(configuration["MYSQL_URL"])
        || ContainsLocalOrPlaceholderDatabase(configuration["DATABASE_URL"]))
    {
        errors.Add("Production database configuration must not point to localhost or use placeholder credentials.");
    }

    if (errors.Count > 0)
    {
        throw new InvalidOperationException("Production configuration is incomplete: " + string.Join("; ", errors));
    }
}

static void Require(string? value, string name, ICollection<string> errors, int minLength = 1)
{
    if (string.IsNullOrWhiteSpace(value) || value.Length < minLength)
    {
        errors.Add($"{name} must be configured with at least {minLength} characters.");
    }
}

static void RequireAny(IEnumerable<string?> values, string name, ICollection<string> errors)
{
    if (!values.Any(value => !string.IsNullOrWhiteSpace(value)))
    {
        errors.Add($"{name} must be configured.");
    }
}

static bool ContainsLocalOrPlaceholderDatabase(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return false;
    }

    var lowered = value.ToLowerInvariant();
    return lowered.Contains("localhost")
        || lowered.Contains("127.0.0.1")
        || lowered.Contains("your_password")
        || lowered.Contains("password=password")
        || lowered.Contains("password=root");
}

static bool IsHttpOrHttps(Uri uri)
{
    return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
}
