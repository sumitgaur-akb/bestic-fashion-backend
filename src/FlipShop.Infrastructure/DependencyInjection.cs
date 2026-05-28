using FlipShop.Application.Interfaces;
using FlipShop.Domain.Common;
using FlipShop.Infrastructure.Data;
using FlipShop.Infrastructure.Repositories;
using FlipShop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlipShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Server=localhost;Database=flipshop;User=root;Password=password;";
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
}
