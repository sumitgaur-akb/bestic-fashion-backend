using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using FlipShop.Application.Common;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Domain.Enums;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FlipShop.Infrastructure.Services;

public sealed class AuthService(AppDbContext dbContext, IConfiguration configuration, IOtpService otpService, IEmailService emailService) : IAuthService
{
    public async Task<ApiResponse<AuthResult>> RegisterCustomerAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (await dbContext.Users.AnyAsync(x => x.Email == request.Email, cancellationToken)) return ApiResponse<AuthResult>.Fail("Email already exists");
        var user = new User { FullName = request.FullName, Email = request.Email, Mobile = request.Mobile, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password) };
        await dbContext.Users.AddAsync(user, cancellationToken);
        await AddRoleAsync(user, UserRoleName.Customer, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await otpService.GenerateAsync(user.Id, user.Email, OtpPurpose.Registration, cancellationToken);
        await emailService.SendRegistrationSuccessAsync(user.Email, user.FullName, "Customer", cancellationToken);
        return ApiResponse<AuthResult>.Ok(await BuildAuthResultAsync(user, cancellationToken), "Customer registered");
    }

    public async Task<ApiResponse<AuthResult>> RegisterSellerAsync(SellerRegisterRequest request, CancellationToken cancellationToken)
    {
        var validationErrors = ValidateSellerRegister(request);
        if (validationErrors.Count > 0) return ApiResponse<AuthResult>.Fail("Seller registration validation failed", validationErrors);
        var otpVerified = await dbContext.OtpVerifications.AnyAsync(x => x.Destination == request.Email && x.Purpose == OtpPurpose.Registration && x.VerifiedAt != null && x.VerifiedAt > DateTime.UtcNow.AddMinutes(-30), cancellationToken);
        if (!otpVerified) return ApiResponse<AuthResult>.Fail("Please verify seller email OTP before registration");
        if (await dbContext.Users.AnyAsync(x => x.Email == request.Email, cancellationToken)) return ApiResponse<AuthResult>.Fail("Email already exists");
        var user = new User { FullName = request.FullName, Email = request.Email, Mobile = request.Mobile, PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password) };
        await dbContext.Users.AddAsync(user, cancellationToken);
        await AddRoleAsync(user, UserRoleName.Seller, cancellationToken);
        user.EmailVerified = true;
        await dbContext.Sellers.AddAsync(new Seller { User = user, DisplayName = request.StoreName, Status = SellerStatus.PendingKyc }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await emailService.SendRegistrationSuccessAsync(user.Email, user.FullName, "Seller", cancellationToken);
        return ApiResponse<AuthResult>.Ok(await BuildAuthResultAsync(user, cancellationToken), "Seller registered. Complete KYC for admin approval");
    }

    public async Task<ApiResponse<AuthResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == request.EmailOrMobile || x.Mobile == request.EmailOrMobile, cancellationToken);
        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) return ApiResponse<AuthResult>.Fail("Invalid credentials");
        return ApiResponse<AuthResult>.Ok(await BuildAuthResultAsync(user, cancellationToken), "Logged in");
    }

    public async Task<ApiResponse<string>> SendLoginOtpAsync(LoginWithOtpRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Email == request.Destination || x.Mobile == request.Destination, cancellationToken);
        await otpService.GenerateAsync(user?.Id, request.Destination, request.Purpose, cancellationToken);
        return ApiResponse<string>.Ok("OTP sent");
    }

    public async Task<ApiResponse<AuthResult>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        if (!await otpService.VerifyAsync(request.Destination, request.Otp, request.Purpose, cancellationToken)) return ApiResponse<AuthResult>.Fail("Invalid or expired OTP");
        var user = await dbContext.Users.Include(x => x.UserRoles).ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Email == request.Destination || x.Mobile == request.Destination, cancellationToken);
        if (user is null) return ApiResponse<AuthResult>.Fail("User not found");
        if (request.Destination.Contains('@')) user.EmailVerified = true; else user.MobileVerified = true;

        if (request.Purpose == OtpPurpose.SellerLogin)
        {
            var seller = await dbContext.Sellers.FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);
            if (seller is not null && seller.Status == SellerStatus.PendingOtp)
            {
                seller.Status = SellerStatus.PendingKyc;
                await emailService.SendRegistrationSuccessAsync(user.Email, user.FullName, "Seller", cancellationToken);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<AuthResult>.Ok(await BuildAuthResultAsync(user, cancellationToken), "OTP verified");
    }

    public async Task<ApiResponse<string>> VerifyPreRegistrationOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken)
    {
        if (request.Purpose != OtpPurpose.Registration) return ApiResponse<string>.Fail("Invalid OTP purpose");
        if (!await otpService.VerifyAsync(request.Destination, request.Otp, request.Purpose, cancellationToken)) return ApiResponse<string>.Fail("Invalid or expired OTP");
        return ApiResponse<string>.Ok("Email OTP verified");
    }

    public async Task<ApiResponse<string>> ResendOtpAsync(ResendOtpRequest request, CancellationToken cancellationToken)
    {
        await otpService.GenerateAsync(null, request.Destination, request.Purpose, cancellationToken);
        return ApiResponse<string>.Ok("OTP resent");
    }

    private async Task AddRoleAsync(User user, UserRoleName roleName, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.FirstAsync(x => x.Name == roleName.ToString(), cancellationToken);
        user.UserRoles.Add(new UserRole { User = user, Role = role });
    }

    private static List<string> ValidateSellerRegister(SellerRegisterRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Length < 3 || !Regex.IsMatch(request.FullName, "^[A-Za-z ]+$")) errors.Add("Owner name must contain only letters and spaces, minimum 3 characters.");
        if (string.IsNullOrWhiteSpace(request.StoreName) || request.StoreName.Length < 3) errors.Add("Store name must be at least 3 characters.");
        if (string.IsNullOrWhiteSpace(request.Email) || !Regex.IsMatch(request.Email, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$")) errors.Add("Valid email is required.");
        if (string.IsNullOrWhiteSpace(request.Mobile) || !Regex.IsMatch(request.Mobile, "^[6-9][0-9]{9}$")) errors.Add("Valid 10 digit mobile number is required.");
        if (string.IsNullOrWhiteSpace(request.Password) || !Regex.IsMatch(request.Password, "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)(?=.*[^A-Za-z0-9]).{8,}$")) errors.Add("Password must contain uppercase, lowercase, number, special character and minimum 8 characters.");
        return errors;
    }

    private async Task<AuthResult> BuildAuthResultAsync(User user, CancellationToken cancellationToken)
    {
        await dbContext.Entry(user).Collection(x => x.UserRoles).Query().Include(x => x.Role).LoadAsync(cancellationToken);
        var roles = user.UserRoles.Select(x => x.Role.Name).ToArray();
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiresAt = DateTime.UtcNow.AddDays(14);
        await dbContext.SaveChangesAsync(cancellationToken);
        var sellerStatus = await dbContext.Sellers.AsNoTracking().Where(x => x.UserId == user.Id).Select(x => x.Status.ToString()).FirstOrDefaultAsync(cancellationToken);
        return new AuthResult(user.Id, user.FullName, user.Email, roles, CreateJwt(user, roles), refreshToken, sellerStatus);
    }

    private string CreateJwt(User user, IReadOnlyList<string> roles)
    {
        var jwtKey = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key must be configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Email, user.Email), new(ClaimTypes.Name, user.FullName) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var sellerStatus = dbContext.Sellers.AsNoTracking().Where(x => x.UserId == user.Id).Select(x => x.Status.ToString()).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(sellerStatus)) claims.Add(new Claim("seller_status", sellerStatus));
        var token = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, expires: DateTime.UtcNow.AddHours(2), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
