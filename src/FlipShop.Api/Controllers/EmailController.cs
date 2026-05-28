using FlipShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

[Authorize(Roles = "Admin")]
public sealed class EmailController(IEmailService emailService) : ApiControllerBase
{
    [HttpPost("otp")]
    public async Task<IActionResult> SendOtp(string recipient, string otp, CancellationToken ct)
    {
        await emailService.SendOtpAsync(recipient, otp, ct);
        return Accepted(new { message = "OTP email queued/sent" });
    }
}
