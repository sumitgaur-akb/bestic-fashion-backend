using FlipShop.Domain.Entities;
using FlipShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FlipShop.Infrastructure.Data;

public static class DevelopmentDataSeeder
{
    public static async Task SeedAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var customerRole = await EnsureRoleAsync(dbContext, UserRoleName.Customer, cancellationToken);
        var sellerRole = await EnsureRoleAsync(dbContext, UserRoleName.Seller, cancellationToken);
        var adminRole = await EnsureRoleAsync(dbContext, UserRoleName.Admin, cancellationToken);

        var sellerUser = await EnsureUserAsync(dbContext, "Demo Seller", "seller@flipshop.local", "8888888888", sellerRole, cancellationToken);
        var onboardingSellerUser = await EnsureUserAsync(dbContext, "Onboarding Seller", "seller-onboarding@flipshop.local", "8666666666", sellerRole, cancellationToken);
        var customerUser = await EnsureUserAsync(dbContext, "Demo Customer", "customer@flipshop.local", "7777777777", customerRole, cancellationToken);
        var adminUser = await EnsureUserAsync(dbContext, "Admin User", "admin@flipshop.local", "9999999999", adminRole, cancellationToken);
        if (!await dbContext.Sellers.AnyAsync(x => x.UserId == onboardingSellerUser.Id, cancellationToken))
        {
            await dbContext.Sellers.AddAsync(new Seller { User = onboardingSellerUser, DisplayName = "Onboarding Draft Store", Status = SellerStatus.Draft }, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (await dbContext.Products.AnyAsync(x => x.Slug == "flipshop-demo-phone", cancellationToken)) return;

        var seller = await dbContext.Sellers.FirstOrDefaultAsync(x => x.UserId == sellerUser.Id, cancellationToken);
        if (seller is null)
        {
            seller = new Seller
            {
                User = sellerUser,
                DisplayName = "Demo Seller Store",
                Status = SellerStatus.Approved,
                BusinessDetails = new SellerBusinessDetails
                {
                    BusinessName = "Demo Seller Store",
                    LegalBusinessName = "Demo Seller Store Private Limited",
                    BusinessType = BusinessType.PrivateLimited,
                    GstNumber = "GST-DEMO",
                    BusinessAddress = "Demo Street, Bengaluru",
                    Pincode = "560001"
                },
                BankDetails = new SellerBankDetails { BankName = "Demo Bank", AccountHolderName = "Demo Seller", AccountNumberMasked = "XXXX1234", AccountNumberLast4 = "1234" },
                Warehouses = [new Warehouse { Name = "Primary Warehouse", Address = "Demo Street, Bengaluru", ContactPerson = "Demo Seller", ContactNumber = "8888888888", Pincode = "560001" }]
            };
            await dbContext.Sellers.AddAsync(seller, cancellationToken);
        }

        var category = await dbContext.Set<Category>().FirstOrDefaultAsync(x => x.Slug == "mobiles", cancellationToken);
        if (category is null)
        {
            category = new Category { Name = "Mobiles", Slug = "mobiles" };
            await dbContext.Set<Category>().AddAsync(category, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        var subCategory = await dbContext.Set<SubCategory>().FirstOrDefaultAsync(x => x.CategoryId == category.Id && x.Slug == "smartphones", cancellationToken);
        if (subCategory is null)
        {
            subCategory = new SubCategory { Category = category, Name = "Smartphones", Slug = "smartphones" };
            await dbContext.Set<SubCategory>().AddAsync(subCategory, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        var product = new Product
        {
            Seller = seller,
            Category = category,
            SubCategory = subCategory,
            Title = "FlipShop Demo Phone",
            Slug = "flipshop-demo-phone",
            Description = "Seed product for local API testing.",
            Brand = "FlipShop",
            BasePrice = 15999,
            DiscountPrice = 13999,
            ApprovalStatus = ProductApprovalStatus.Approved,
            AverageRating = 4.4m,
            RatingCount = 128,
            Images = [new ProductImage { ImageUrl = "/assets/demo-phone.jpg", IsPrimary = true, AltText = "Demo phone" }],
            Variants =
            [
                new ProductVariant
                {
                    Sku = "DEMO-PHONE-BLACK-128",
                    Color = "Black",
                    Size = "128 GB",
                    Price = 13999,
                    Stock = new ProductStock { Quantity = 25, LowStockThreshold = 5 }
                }
            ]
        };

        var address = new Address
        {
            User = customerUser,
            FullName = "Demo Customer",
            Phone = "7777777777",
            Line1 = "Demo Street",
            City = "Bengaluru",
            State = "Karnataka",
            PostalCode = "560001",
            IsDefault = true
        };

        await dbContext.Products.AddAsync(product, cancellationToken);
        await dbContext.Set<Address>().AddAsync(address, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<Role> EnsureRoleAsync(AppDbContext dbContext, UserRoleName roleName, CancellationToken cancellationToken)
    {
        var name = roleName.ToString();
        var role = await dbContext.Roles.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
        if (role is not null) return role;
        role = new Role { Name = name };
        await dbContext.Roles.AddAsync(role, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return role;
    }

    private static async Task<User> EnsureUserAsync(AppDbContext dbContext, string fullName, string email, string mobile, Role role, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.Include(x => x.UserRoles).FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
        if (user is null)
        {
            user = new User { FullName = fullName, Email = email, Mobile = mobile, EmailVerified = true, MobileVerified = true };
            await dbContext.Users.AddAsync(user, cancellationToken);
        }
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!");
        if (!user.UserRoles.Any(x => x.RoleId == role.Id || x.Role?.Name == role.Name)) user.UserRoles.Add(new UserRole { User = user, Role = role });
        await dbContext.SaveChangesAsync(cancellationToken);
        return user;
    }
}
