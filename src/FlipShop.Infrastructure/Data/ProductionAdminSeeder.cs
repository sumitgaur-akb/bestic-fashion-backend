using FlipShop.Domain.Entities;
using FlipShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FlipShop.Infrastructure.Data;

public static class ProductionAdminSeeder
{
    public static async Task SeedAsync(
        AppDbContext dbContext,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var email = configuration["AdminBootstrap:Email"]?.Trim();
        var password = configuration["AdminBootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var fullName = configuration["AdminBootstrap:FullName"]?.Trim() ?? "Bestic Fashion Admin";
        var mobile = configuration["AdminBootstrap:Mobile"]?.Trim();
        var roleName = UserRoleName.Admin.ToString();
        var role = await dbContext.Roles.FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);
        if (role is null)
        {
            role = new Role { Name = roleName };
            await dbContext.Roles.AddAsync(role, cancellationToken);
        }

        var user = await dbContext.Users
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                FullName = fullName,
                Email = email,
                Mobile = string.IsNullOrWhiteSpace(mobile) ? null : mobile,
                EmailVerified = true,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
            };
            await dbContext.Users.AddAsync(user, cancellationToken);
        }
        else
        {
            user.FullName = fullName;
            user.EmailVerified = true;
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
        }

        if (!user.UserRoles.Any(x => x.RoleId == role.Id || x.Role?.Name == roleName))
        {
            user.UserRoles.Add(new UserRole { User = user, Role = role });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
