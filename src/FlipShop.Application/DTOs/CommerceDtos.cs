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
public sealed record SellerBusinessInfoRequest(string BusinessName, string LegalBusinessName, BusinessType BusinessType, string? GstNumber, string? PanNumber, string? CinNumber, string BusinessAddress, string Pincode);
public sealed record SellerBankInfoRequest(string AccountHolderName, string BankName, string AccountNumber, string IfscCode);
public sealed record WarehouseRequest(long? Id, string Name, string Address, string ContactPerson, string ContactNumber, string Pincode);
public sealed record SellerDocumentRequest(SellerDocumentType DocumentType, string FileName, string FileUrl, string? ContentType);
public sealed record SellerOnboardingRequest(
    SellerBusinessInfoRequest Business,
    SellerBankInfoRequest Bank,
    IReadOnlyList<WarehouseRequest> Warehouses,
    IReadOnlyList<SellerDocumentRequest> Documents);
public sealed record SellerOnboardingStatusDto(string Status, SellerBusinessInfoRequest? Business, SellerBankInfoRequest? Bank, IReadOnlyList<WarehouseRequest> Warehouses, IReadOnlyList<SellerDocumentRequest> Documents, string? ReviewNotes);
public sealed record SellerReviewDto(long SellerId, long UserId, string OwnerName, string Email, string StoreName, string Status, string? BusinessName, string? LegalBusinessName, string? BusinessType, string? GstNumber, string? PanNumber, string? PickupAddress, string? BankName, string? AccountHolderName, DateTime CreatedAt, int ProductCount, int TotalOrders, decimal TotalRevenue, int DocumentCount, int WarehouseCount);
public sealed record AdminSellerSummaryDto(int TotalSellers, int PendingOtp, int PendingKyc, int PendingApproval, int Approved, int RejectedOrSuspended, decimal MarketplaceSellerRevenue);
public sealed record SellerApprovalRequest(bool Approved, string? Notes, bool RequestAdditionalDocuments = false);
