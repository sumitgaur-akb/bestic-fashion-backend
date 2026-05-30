using FlipShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FlipShop.Infrastructure.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Seller> Sellers => Set<Seller>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<SellerDocument> SellerDocuments => Set<SellerDocument>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ProductStock> ProductStock => Set<ProductStock>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistory => Set<OrderStatusHistory>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();
    public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        ConfigureDatabaseNaming(modelBuilder);
        modelBuilder.Entity<User>().HasIndex(x => x.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(x => x.Mobile).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(x => x.Name).IsUnique();
        modelBuilder.Entity<Warehouse>().HasIndex(x => new { x.SellerId, x.Name });
        modelBuilder.Entity<SellerDocument>().HasIndex(x => new { x.SellerId, x.DocumentType }).IsUnique();
        modelBuilder.Entity<Product>().HasIndex(x => x.Slug).IsUnique();
        modelBuilder.Entity<ProductVariant>().HasIndex(x => x.Sku).IsUnique();
        modelBuilder.Entity<UserRole>().HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();
        modelBuilder.Entity<CartItem>().HasIndex(x => new { x.CartId, x.ProductVariantId }).IsUnique();
    }

    private static void ConfigureDatabaseNaming(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Role>().ToTable("roles");
        modelBuilder.Entity<UserRole>().ToTable("user_roles");
        modelBuilder.Entity<Seller>().ToTable("sellers");
        modelBuilder.Entity<SellerBusinessDetails>().ToTable("seller_business_details");
        modelBuilder.Entity<SellerBankDetails>().ToTable("seller_bank_details");
        modelBuilder.Entity<Warehouse>().ToTable("warehouses");
        modelBuilder.Entity<SellerDocument>().ToTable("seller_documents");
        modelBuilder.Entity<Category>().ToTable("categories");
        modelBuilder.Entity<SubCategory>().ToTable("sub_categories");
        modelBuilder.Entity<Product>().ToTable("products");
        modelBuilder.Entity<ProductImage>().ToTable("product_images");
        modelBuilder.Entity<ProductVariant>().ToTable("product_variants");
        modelBuilder.Entity<ProductStock>().ToTable("product_stock");
        modelBuilder.Entity<Cart>().ToTable("carts");
        modelBuilder.Entity<CartItem>().ToTable("cart_items");
        modelBuilder.Entity<Wishlist>().ToTable("wishlists");
        modelBuilder.Entity<WishlistItem>().ToTable("wishlist_items");
        modelBuilder.Entity<Address>().ToTable("addresses");
        modelBuilder.Entity<Order>().ToTable("orders");
        modelBuilder.Entity<OrderItem>().ToTable("order_items");
        modelBuilder.Entity<OrderStatusHistory>().ToTable("order_status_history");
        modelBuilder.Entity<Payment>().ToTable("payments");
        modelBuilder.Entity<OtpVerification>().ToTable("otp_verifications");
        modelBuilder.Entity<EmailLog>().ToTable("email_logs");
        modelBuilder.Entity<Notification>().ToTable("notifications");
        modelBuilder.Entity<Review>().ToTable("reviews");
        modelBuilder.Entity<Rating>().ToTable("ratings");

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
                if (property.ClrType.IsEnum)
                {
                    property.SetValueConverter(typeof(EnumToStringConverter<>).MakeGenericType(property.ClrType));
                }
            }

            foreach (var key in entity.GetKeys())
            {
                key.SetName(ToSnakeCase(key.GetName() ?? string.Empty));
            }

            foreach (var index in entity.GetIndexes())
            {
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName() ?? string.Empty));
            }

            foreach (var foreignKey in entity.GetForeignKeys())
            {
                foreignKey.SetConstraintName(ToSnakeCase(foreignKey.GetConstraintName() ?? string.Empty));
            }
        }
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var builder = new System.Text.StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (char.IsUpper(current))
            {
                if (i > 0 && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]) || (i + 1 < value.Length && char.IsLower(value[i + 1]))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }
}
