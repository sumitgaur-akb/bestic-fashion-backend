using FlipShop.Domain.Enums;

namespace FlipShop.Application.DTOs;

public sealed record RegisterRequest(string FullName, string Email, string? Mobile, string Password);
public sealed record SellerRegisterRequest(string FullName, string Email, string? Mobile, string Password, string StoreName);
public sealed record LoginRequest(string EmailOrMobile, string Password);
public sealed record LoginWithOtpRequest(string Destination, OtpPurpose Purpose);
public sealed record VerifyOtpRequest(string Destination, string Otp, OtpPurpose Purpose);
public sealed record ResendOtpRequest(string Destination, OtpPurpose Purpose);
public sealed record RefreshTokenRequest(string RefreshToken);
public sealed record AuthResult(long UserId, string FullName, string Email, IReadOnlyList<string> Roles, string AccessToken, string RefreshToken, string? SellerStatus = null);
