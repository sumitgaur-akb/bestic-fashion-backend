using FlipShop.Application.Common;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Domain.Enums;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlipShop.Infrastructure.Services;

public sealed class OrderService(AppDbContext dbContext, IEmailService emailService) : IOrderService
{
    public async Task<ApiResponse<string>> SendConfirmationOtpAsync(long userId, CancellationToken cancellationToken)
    {
        var cart = await dbContext.Carts.Include(x => x.User).Include(x => x.Items).ThenInclude(x => x.ProductVariant).ThenInclude(x => x.Product).FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (cart is null || cart.Items.Count == 0) return ApiResponse<string>.Fail("Cart is empty");
        var otp = Random.Shared.Next(100000, 999999).ToString();
        await dbContext.OtpVerifications.AddAsync(new OtpVerification
        {
            UserId = userId,
            Destination = cart.User.Email,
            Purpose = OtpPurpose.OrderConfirmation,
            OtpHash = BCrypt.Net.BCrypt.HashPassword(otp),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await emailService.SendOrderConfirmationOtpAsync(cart.User.Email, otp, ToCartDto(cart), cancellationToken);
        return ApiResponse<string>.Ok("OTP sent", "Order confirmation OTP sent");
    }

    public async Task<ApiResponse<OrderDto>> PlaceOrderAsync(long userId, CheckoutRequest request, CancellationToken cancellationToken)
    {
        var cart = await dbContext.Carts.Include(x => x.User).Include(x => x.Items).ThenInclude(x => x.ProductVariant).ThenInclude(x => x.Stock).Include(x => x.Items).ThenInclude(x => x.ProductVariant).ThenInclude(x => x.Product).ThenInclude(x => x.Seller).ThenInclude(x => x.User).FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (cart is null || cart.Items.Count == 0) return ApiResponse<OrderDto>.Fail("Cart is empty");
        if (string.IsNullOrWhiteSpace(request.Otp)) return ApiResponse<OrderDto>.Fail("Order confirmation OTP is required");
        var otp = await dbContext.OtpVerifications
            .Where(x => x.UserId == userId && x.Destination == cart.User.Email && x.Purpose == OtpPurpose.OrderConfirmation && x.VerifiedAt == null && x.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (otp is null || !BCrypt.Net.BCrypt.Verify(request.Otp, otp.OtpHash)) return ApiResponse<OrderDto>.Fail("Invalid or expired order confirmation OTP");
        otp.VerifiedAt = DateTime.UtcNow;
        var addressId = request.AddressId;
        if (addressId <= 0)
        {
            addressId = await dbContext.Set<Address>()
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Id)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }
        if (!await dbContext.Set<Address>().AnyAsync(x => x.Id == addressId && x.UserId == userId, cancellationToken))
            return ApiResponse<OrderDto>.Fail("Please add or select a delivery address before placing the order");

        await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var item in cart.Items)
        {
            if (item.ProductVariant.Stock is null || item.ProductVariant.Stock.Quantity < item.Quantity) return ApiResponse<OrderDto>.Fail($"Insufficient stock for {item.ProductVariant.Sku}");
            item.ProductVariant.Stock.Quantity -= item.Quantity;
        }

        var order = new Order
        {
            UserId = userId,
            AddressId = addressId,
            PaymentMethod = request.PaymentMethod,
            OrderNumber = $"FS{DateTime.UtcNow:yyyyMMddHHmmss}{Random.Shared.Next(100, 999)}",
            TotalAmount = cart.Items.Sum(x => x.Quantity * x.UnitPrice),
            Items = cart.Items.Select(x => new OrderItem { SellerId = x.ProductVariant.Product.SellerId, ProductId = x.ProductVariant.ProductId, ProductVariantId = x.ProductVariantId, Quantity = x.Quantity, UnitPrice = x.UnitPrice, LineTotal = x.Quantity * x.UnitPrice }).ToList()
        };
        await dbContext.Orders.AddAsync(order, cancellationToken);
        dbContext.CartItems.RemoveRange(cart.Items);
        await dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        var productLines = cart.Items.Select(x => $"{x.ProductVariant.Product.Title} x {x.Quantity} - Rs {x.Quantity * x.UnitPrice}").ToArray();
        var sellerIds = order.Items.Select(x => x.SellerId).Distinct().ToHashSet();
        var sellerEmails = (await dbContext.Sellers.Include(x => x.User).ToListAsync(cancellationToken))
            .Where(x => sellerIds.Contains(x.Id))
            .Select(x => x.User.Email)
            .ToList();
        await emailService.SendOrderPlacedAsync(cart.User.Email, order.OrderNumber, productLines, order.TotalAmount, cancellationToken);
        foreach (var sellerEmail in sellerEmails) await emailService.SendSellerOrderReceivedAsync(sellerEmail, order.OrderNumber, productLines, order.TotalAmount, cancellationToken);
        return ApiResponse<OrderDto>.Ok(ToDto(order), "Order placed");
    }

    public async Task<ApiResponse<IReadOnlyList<OrderDto>>> GetCustomerOrdersAsync(long userId, CancellationToken cancellationToken)
    {
        var orders = await dbContext.Orders.Where(x => x.UserId == userId).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<OrderDto>>.Ok(orders.Select(ToDto).ToArray());
    }

    public async Task<ApiResponse<IReadOnlyList<OrderDto>>> GetSellerOrdersAsync(long sellerUserId, CancellationToken cancellationToken)
    {
        var seller = await dbContext.Sellers.FirstOrDefaultAsync(x => x.UserId == sellerUserId, cancellationToken);
        if (seller is null) return ApiResponse<IReadOnlyList<OrderDto>>.Fail("Seller not found");
        if (seller.Status != SellerStatus.Approved) return ApiResponse<IReadOnlyList<OrderDto>>.Fail("Seller approval is required");
        var orders = await dbContext.Orders.Where(x => x.Items.Any(i => i.SellerId == seller.Id)).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<OrderDto>>.Ok(orders.Select(ToDto).ToArray());
    }

    public async Task<ApiResponse<string>> UpdateStatusAsync(long sellerUserId, long orderId, OrderStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        var seller = await dbContext.Sellers.FirstOrDefaultAsync(x => x.UserId == sellerUserId, cancellationToken);
        var order = await dbContext.Orders.Include(x => x.User).Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (seller is null || order is null) return ApiResponse<string>.Fail("Order not found");
        if (seller.Status != SellerStatus.Approved) return ApiResponse<string>.Fail("Seller approval is required");
        foreach (var item in order.Items.Where(x => x.SellerId == seller.Id && (!request.OrderItemId.HasValue || x.Id == request.OrderItemId))) item.SellerOrderStatus = request.Status;
        order.OrderStatus = request.Status;
        await dbContext.OrderStatusHistory.AddAsync(new OrderStatusHistory { OrderId = order.Id, OrderItemId = request.OrderItemId, Status = request.Status.ToString(), Notes = request.Notes }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await emailService.SendOrderStatusChangedAsync(order.User.Email, order.OrderNumber, request.Status.ToString(), cancellationToken);
        return ApiResponse<string>.Ok("Order status updated");
    }

    public async Task<ApiResponse<string>> CancelAsync(long userId, long orderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, cancellationToken);
        if (order is null) return ApiResponse<string>.Fail("Order not found");
        order.OrderStatus = OrderStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
        await emailService.SendOrderCancelledAsync(order.User.Email, order.OrderNumber, cancellationToken);
        return ApiResponse<string>.Ok("Order cancelled");
    }

    private static OrderDto ToDto(Order x) => new(x.Id, x.OrderNumber, x.TotalAmount, x.OrderStatus.ToString(), x.PaymentStatus.ToString(), x.CreatedAt);
    private static CartDto ToCartDto(Cart cart)
    {
        var items = cart.Items.Select(x => new CartItemDto(x.Id, x.ProductVariantId, x.ProductVariant.Product.Title, x.Quantity, x.UnitPrice, x.Quantity * x.UnitPrice)).ToArray();
        return new CartDto(cart.Id, items, items.Sum(x => x.LineTotal));
    }
}
