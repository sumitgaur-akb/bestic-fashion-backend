using FlipShop.Application.Interfaces;
using FlipShop.Domain.Common;
using FlipShop.Infrastructure.Data;
using FlipShop.Infrastructure.Repositories;
using FlipShop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace FlipShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = GetConfiguredConnectionString(configuration);
        services.AddDbContext<AppDbContext>(options =>
        {
            if (configuration.GetValue<bool>("UseInMemoryDatabase"))
            {
                options
                    .UseInMemoryDatabase("FlipShopLocal")
                    .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
                return;
            }

            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
        });
        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOtpService, OtpService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<ISellerDashboardService, SellerDashboardService>();
        services.AddScoped<ISellerManagementService, SellerManagementService>();
        return services;
    }

    private static string GetConfiguredConnectionString(IConfiguration configuration)
    {
        var defaultConnection = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrWhiteSpace(defaultConnection))
        {
            return defaultConnection;
        }

        var mysqlAddonUri = configuration["MYSQL_ADDON_URI"];
        if (!string.IsNullOrWhiteSpace(mysqlAddonUri))
        {
            return BuildConnectionStringFromUri(mysqlAddonUri);
        }

        var host = configuration["MYSQL_ADDON_HOST"];
        var database = configuration["MYSQL_ADDON_DB"];
        var user = configuration["MYSQL_ADDON_USER"];
        var password = configuration["MYSQL_ADDON_PASSWORD"];
        if (!string.IsNullOrWhiteSpace(host)
            && !string.IsNullOrWhiteSpace(database)
            && !string.IsNullOrWhiteSpace(user)
            && !string.IsNullOrWhiteSpace(password))
        {
            _ = uint.TryParse(configuration["MYSQL_ADDON_PORT"], out var port);
            return new MySqlConnectionStringBuilder
            {
                Server = host,
                Port = port == 0 ? 3306u : port,
                Database = database,
                UserID = user,
                Password = password,
                SslMode = MySqlSslMode.Preferred,
            }.ConnectionString;
        }

        return "Server=localhost;Database=flipshop;User=root;Password=password;";
    }

    private static string BuildConnectionStringFromUri(string mysqlUri)
    {
        var uri = new Uri(mysqlUri);
        var userInfo = uri.UserInfo.Split(':', 2);

        return new MySqlConnectionStringBuilder
        {
            Server = uri.Host,
            Port = uri.Port > 0 ? (uint)uri.Port : 3306u,
            Database = uri.AbsolutePath.TrimStart('/'),
            UserID = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            SslMode = MySqlSslMode.Preferred,
        }.ConnectionString;
    }
}
