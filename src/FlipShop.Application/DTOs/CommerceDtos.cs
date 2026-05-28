using FlipShop.Domain.Enums;

namespace FlipShop.Application.DTOs;

public sealed record CartItemRequest(long ProductVariantId, int Quantity);
public sealed record CartItemDto(long Id, long ProductVariantId, string ProductTitle, int Quantity, decimal UnitPrice, decimal LineTotal);
public sealed record CartDto(long Id, IReadOnlyList<CartItemDto> Items, decimal Total);
public sealed record AddressRequest(string FullName, string Phone, string Line1, string? Line2, string City, string State, string PostalCode, bool IsDefault);
public sealed record AddressDto(long Id, string FullName, string Phone, string Line1, string? Line2, string City, string State, string PostalCode, bool IsDefault);
public sealed record CheckoutRequest(long AddressId, PaymentMethod PaymentMethod, string? Otp = null);
public sealed record OrderDto(long Id, string OrderNumber, decimal TotalAmount, string OrderStatus, string PaymentStatus, DateTime CreatedAt);
public sealed record OrderStatusUpdateRequest(long? OrderItemId, OrderStatus Status, string? Notes);
public sealed record DashboardSummaryDto(int TotalOrders, int PendingOrders, int CompletedOrders, int CancelledOrders, decimal TotalRevenue, decimal TodaySales, int ProductCount, int LowStockProducts);
public sealed record RevenuePointDto(DateOnly Date, decimal Revenue);
public sealed record SellerOnboardingRequest(string BusinessName, string? GstNumber, string? PanNumber, string? PickupAddress, string? BankName, string? AccountHolderName, string? AccountNumberMasked, string? IfscCode);
public sealed record SellerReviewDto(long SellerId, long UserId, string OwnerName, string Email, string StoreName, string Status, string? BusinessName, string? GstNumber, string? PanNumber, string? PickupAddress, string? BankName, string? AccountHolderName, DateTime CreatedAt, int ProductCount, int TotalOrders, decimal TotalRevenue);
public sealed record AdminSellerSummaryDto(int TotalSellers, int PendingOtp, int PendingKyc, int PendingApproval, int Approved, int RejectedOrSuspended, decimal MarketplaceSellerRevenue);
public sealed record SellerApprovalRequest(bool Approved, string? Notes);
