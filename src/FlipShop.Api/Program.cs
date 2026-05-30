using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using FlipShop.Api.Middleware;
using FlipShop.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

ValidateProductionConfiguration(builder.Configuration, builder.Environment);

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
        if (allowedOrigins.Contains("*"))
        {
            if (builder.Environment.IsProduction())
            {
                throw new InvalidOperationException("Wildcard CORS is not allowed in production. Configure Cors:AllowedOrigins with explicit HTTPS origins.");
            }

            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
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

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key must be configured.");
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

    return distinctOrigins.Length > 0
        ? distinctOrigins
        : ["http://localhost:4200", "http://127.0.0.1:4200"];
}

static IEnumerable<string> SplitOrigins(string? value)
{
    return string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

static void ValidateProductionConfiguration(IConfiguration configuration, IWebHostEnvironment environment)
{
    if (!environment.IsProduction())
    {
        return;
    }

    var errors = new List<string>();
    var jwtKey = configuration["Jwt:Key"];
    Require(jwtKey, "Jwt:Key", errors, minLength: 32);
    if (jwtKey?.Contains("replace-with", StringComparison.OrdinalIgnoreCase) == true)
    {
        errors.Add("Jwt:Key must not use the checked-in placeholder value.");
    }

    var defaultConnection = configuration.GetConnectionString("DefaultConnection");
    RequireAny(
        [
            defaultConnection,
            configuration["MYSQL_ADDON_URI"],
            configuration["MYSQL_ADDON_HOST"]
        ],
        "ConnectionStrings:DefaultConnection or MYSQL_ADDON_URI",
        errors);
    RequireAny(
        [
            configuration["Cors:AllowedOrigins"],
            .. configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []
        ],
        "Cors:AllowedOrigins",
        errors);

    if (GetConfiguredOrigins(configuration).Contains("*"))
    {
        errors.Add("Cors:AllowedOrigins cannot contain * in production.");
    }

    var origins = GetConfiguredOrigins(configuration);
    if (origins.Any(origin => !Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
    {
        errors.Add("Cors:AllowedOrigins must contain only absolute HTTPS origins in production.");
    }

    if (ContainsLocalOrPlaceholderDatabase(defaultConnection) || ContainsLocalOrPlaceholderDatabase(configuration["MYSQL_ADDON_URI"]))
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
