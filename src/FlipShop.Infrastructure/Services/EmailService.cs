using System.Net;
using System.Net.Mail;
using System.Net.Http.Json;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Domain.Enums;
using FlipShop.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FlipShop.Infrastructure.Services;

public sealed class EmailService(
    IConfiguration configuration,
    AppDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    ILogger<EmailService> logger) : IEmailService
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
            var brevoApiKey = configuration["Brevo:ApiKey"];
            if (!string.IsNullOrWhiteSpace(brevoApiKey))
            {
                await SendWithBrevoAsync(recipient, subject, html, brevoApiKey, cancellationToken);
                log.Status = EmailStatus.Sent;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var host = configuration["Smtp:Host"];
            if (string.IsNullOrWhiteSpace(host))
            {
                log.Status = EmailStatus.Queued;
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            var username = configuration["Smtp:Username"];
            var password = configuration["Smtp:Password"];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("SMTP username and password must be configured.");
            }

            using var client = new SmtpClient(host, int.Parse(configuration["Smtp:Port"] ?? "587"))
            {
                EnableSsl = bool.Parse(configuration["Smtp:EnableSsl"] ?? "true"),
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(username, password)
            };
            var from = configuration["Smtp:From"];
            if (string.IsNullOrWhiteSpace(from) || from.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            {
                from = username;
            }

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
            logger.LogError(ex, "SMTP email delivery failed for {Recipient} using template {TemplateKey}", recipient, templateKey);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task SendWithBrevoAsync(
        string recipient,
        string subject,
        string html,
        string apiKey,
        CancellationToken cancellationToken)
    {
        var senderEmail = configuration["Brevo:SenderEmail"] ?? configuration["Smtp:From"] ?? configuration["Smtp:Username"];
        if (string.IsNullOrWhiteSpace(senderEmail))
        {
            throw new InvalidOperationException("Brevo sender email must be configured.");
        }

        var senderName = configuration["Brevo:SenderName"] ?? configuration["Smtp:AdminName"] ?? "Bestic Fashion";
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
        request.Headers.Add("api-key", apiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new { email = senderEmail, name = senderName },
            to = new[] { new { email = recipient } },
            subject,
            htmlContent = html
        });

        var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"Brevo email API returned {(int)response.StatusCode}: {error}");
        }
    }
}
