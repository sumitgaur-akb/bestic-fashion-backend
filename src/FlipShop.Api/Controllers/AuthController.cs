using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

public sealed class AuthController(IAuthService authService) : ApiControllerBase
{
    [HttpPost("customer/register")]
    public Task<IActionResult> RegisterCustomer(RegisterRequest request, CancellationToken ct) => Wrap(authService.RegisterCustomerAsync(request, ct));

    [HttpPost("seller/register")]
    public Task<IActionResult> RegisterSeller(SellerRegisterRequest request, CancellationToken ct) => Wrap(authService.RegisterSellerAsync(request, ct));

    [HttpPost("login")]
    public Task<IActionResult> Login(LoginRequest request, CancellationToken ct) => Wrap(authService.LoginAsync(request, ct));

    [HttpPost("login-otp")]
    public Task<IActionResult> LoginOtp(LoginWithOtpRequest request, CancellationToken ct) => Wrap(authService.SendLoginOtpAsync(request, ct));

    [HttpPost("verify-otp")]
    public Task<IActionResult> VerifyOtp(VerifyOtpRequest request, CancellationToken ct) => Wrap(authService.VerifyOtpAsync(request, ct));

    [HttpPost("verify-registration-otp")]
    public Task<IActionResult> VerifyRegistrationOtp(VerifyOtpRequest request, CancellationToken ct) => Wrap(authService.VerifyPreRegistrationOtpAsync(request, ct));

    [HttpPost("resend-otp")]
    public Task<IActionResult> ResendOtp(ResendOtpRequest request, CancellationToken ct) => Wrap(authService.ResendOtpAsync(request, ct));

    [HttpPost("refresh-token")]
    public IActionResult RefreshToken(RefreshTokenRequest request) => Accepted(new { message = "Refresh-token rotation placeholder", request.RefreshToken });

    [HttpPost("logout")]
    public IActionResult Logout() => Ok(new { message = "Logout placeholder: revoke refresh token in production" });

}
