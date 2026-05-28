using System.Net;
using System.Net.Mail;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Domain.Enums;
using FlipShop.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace FlipShop.Infrastructure.Services;

public sealed class EmailService(IConfiguration configuration, AppDbContext dbContext) : IEmailService
{
    public Task SendOtpAsync(string recipient, string otp, CancellationToken cancellationToken) =>
        SendAsync(recipient, "Your FlipShop OTP", "Otp", EmailTemplateRenderer.Otp(otp), cancellationToken);

    public Task SendOrderConfirmationOtpAsync(string recipient, string otp, CartDto cart, CancellationToken cancellationToken) =>
        SendAsync(recipient, "Confirm your FlipShop order", "OrderConfirmationOtp", EmailTemplateRenderer.OrderConfirmationOtp(otp, cart), cancellationToken);

    public Task SendRegistrationSuccessAsync(string recipient, string name, string role, CancellationToken cancellationToken) =>
        SendAsync(recipient, "Welcome to FlipShop", "Registration", EmailTemplateRenderer.Registration(name, role), cancellationToken);

    public Task SendProductQcResultAsync(string recipient, string productTitle, bool approved, string? notes, string? tags, CancellationToken cancellationToken) =>
        SendAsync(recipient, approved ? $"Product approved: {productTitle}" : $"Product QC failed: {productTitle}", "ProductQc", EmailTemplateRenderer.ProductQc(productTitle, approved, notes, tags), cancellationToken);

    public Task SendOrderPlacedAsync(string recipient, string orderNumber, IReadOnlyList<string> productLines, decimal total, CancellationToken cancellationToken) =>
        SendAsync(recipient, $"Order {orderNumber} placed", "OrderPlaced", EmailTemplateRenderer.OrderPlaced(orderNumber, productLines, total), cancellationToken);

    public Task SendSellerOrderReceivedAsync(string recipient, string orderNumber, IReadOnlyList<string> productLines, decimal total, CancellationToken cancellationToken) =>
        SendAsync(recipient, $"New order {orderNumber}", "SellerOrderReceived", EmailTemplateRenderer.SellerOrder(orderNumber, productLines, total), cancellationToken);

    public Task SendOrderStatusChangedAsync(string recipient, string orderNumber, string status, CancellationToken cancellationToken) =>
        SendAsync(recipient, $"Order {orderNumber} status changed", "OrderStatusChanged", EmailTemplateRenderer.OrderStatus(orderNumber, status), cancellationToken);

    public Task SendOrderCancelledAsync(string recipient, string orderNumber, CancellationToken cancellationToken) =>
        SendAsync(recipient, $"Order {orderNumber} cancelled", "OrderCancelled", EmailTemplateRenderer.Cancelled(orderNumber), cancellationToken);

    private async Task SendAsync(string recipient, string subject, string templateKey, string html, CancellationToken cancellationToken)
    {
        var log = new EmailLog { Recipient = recipient, Subject = subject, TemplateKey = templateKey };
        await dbContext.EmailLogs.AddAsync(log, cancellationToken);

        try
        {
            var host = configuration["Smtp:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                log.Status = EmailStatus.Queued;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            using var client = new SmtpClient(host, int.Parse(configuration["Smtp:Port"] ?? "587"))
            {
                EnableSsl = bool.Parse(configuration["Smtp:EnableSsl"] ?? "true"),
                Credentials = new NetworkCredential(configuration["Smtp:Username"], configuration["Smtp:Password"])
            };
            var from = configuration["Smtp:From"] ?? "admin@flipshop.local";
            var adminName = configuration["Smtp:AdminName"] ?? "FlipShop Admin";
            using var message = new MailMessage(new MailAddress(from, adminName), new MailAddress(recipient))
            {
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            await client.SendMailAsync(message, cancellationToken);
            log.Status = EmailStatus.Sent;
        }
        catch (Exception ex)
        {
            log.Status = EmailStatus.Failed;
            log.ErrorMessage = ex.Message;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
