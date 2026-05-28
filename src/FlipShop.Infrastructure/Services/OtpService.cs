using System.Security.Cryptography;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Domain.Enums;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlipShop.Infrastructure.Services;

public sealed class OtpService(AppDbContext dbContext, IEmailService emailService) : IOtpService
{
    public async Task<string> GenerateAsync(long? userId, string destination, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        var otp = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var record = new OtpVerification
        {
            UserId = userId,
            Destination = destination,
            Purpose = purpose,
            OtpHash = BCrypt.Net.BCrypt.HashPassword(otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };
        await dbContext.OtpVerifications.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        if (destination.Contains('@')) await emailService.SendOtpAsync(destination, otp, cancellationToken);
        return otp;
    }

    public async Task<bool> VerifyAsync(string destination, string otp, OtpPurpose purpose, CancellationToken cancellationToken)
    {
        var record = await dbContext.OtpVerifications
            .Where(x => x.Destination == destination && x.Purpose == purpose && x.VerifiedAt == null && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (record is null || !BCrypt.Net.BCrypt.Verify(otp, record.OtpHash)) return false;
        record.VerifiedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
