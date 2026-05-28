namespace FlipShop.Infrastructure.Services;

using FlipShop.Application.DTOs;
using System.Net;

public static class EmailTemplateRenderer
{
    public static string Otp(string otp) => Wrap("Verification code", $"<p>Your OTP is <strong style=\"font-size:24px\">{otp}</strong>.</p><p>This code expires in 10 minutes.</p>");
    public static string Registration(string name, string role) => Wrap("Welcome to FlipShop", $"<p>Hello {name}, your {role} account was created successfully.</p>");
    public static string OrderConfirmationOtp(string otp, CartDto cart) => Wrap("Confirm your order", $"<p>Your order confirmation OTP is <strong style=\"font-size:24px\">{otp}</strong>.</p>{ProductTable(cart.Items.Select(x => $"{x.ProductTitle} x {x.Quantity} - Rs {x.LineTotal}").ToArray(), cart.Total)}<p>This code expires in 10 minutes.</p>");
    public static string ProductQc(string productTitle, bool approved, string? notes, string? tags) => Wrap(approved ? "Product approved" : "Product QC failed", $"<p><strong>{WebUtility.HtmlEncode(productTitle)}</strong> {(approved ? "passed QC and is live." : "needs correction before listing.")}</p><p><strong>Tagged issues:</strong> {WebUtility.HtmlEncode(tags ?? "None")}</p><p><strong>Reviewer notes:</strong> {WebUtility.HtmlEncode(notes ?? "No notes")}</p>");
    public static string OrderPlaced(string orderNumber, IReadOnlyList<string> productLines, decimal total) => Wrap("Order placed", $"<p>Your order <strong>{orderNumber}</strong> was placed successfully.</p>{ProductTable(productLines, total)}");
    public static string SellerOrder(string orderNumber, IReadOnlyList<string> productLines, decimal total) => Wrap("New seller order", $"<p>You received a new order: <strong>{orderNumber}</strong>.</p>{ProductTable(productLines, total)}");
    public static string OrderStatus(string orderNumber, string status) => Wrap("Order status updated", $"<p>Order <strong>{orderNumber}</strong> is now <strong>{status}</strong>.</p>");
    public static string Cancelled(string orderNumber) => Wrap("Order cancelled", $"<p>Order <strong>{orderNumber}</strong> has been cancelled.</p>");

    private static string ProductTable(IReadOnlyList<string> productLines, decimal total)
    {
        var rows = string.Join("", productLines.Select(x => $"<li>{WebUtility.HtmlEncode(x)}</li>"));
        return $"<ul style=\"line-height:1.7\">{rows}</ul><p><strong>Total:</strong> Rs {total}</p>";
    }

    private static string Wrap(string title, string body) =>
        $"""
        <div style="font-family:Arial,sans-serif;background:#f5f7fb;padding:24px">
          <div style="max-width:640px;margin:auto;background:#fff;border:1px solid #e5e7eb;padding:24px">
            <h2 style="color:#2874f0;margin-top:0">{title}</h2>
            {body}
            <p style="color:#64748b;font-size:13px">FlipShop marketplace notifications</p>
          </div>
        </div>
        """;
}
