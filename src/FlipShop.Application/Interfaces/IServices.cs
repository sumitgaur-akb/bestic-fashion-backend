using FlipShop.Application.Common;
using FlipShop.Application.DTOs;
using FlipShop.Domain.Enums;

namespace FlipShop.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<AuthResult>> RegisterCustomerAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<AuthResult>> RegisterSellerAsync(SellerRegisterRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<AuthResult>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<string>> SendLoginOtpAsync(LoginWithOtpRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<AuthResult>> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<string>> VerifyPreRegistrationOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<string>> ResendOtpAsync(ResendOtpRequest request, CancellationToken cancellationToken);
}

public interface IOtpService
{
    Task<string> GenerateAsync(long? userId, string destination, OtpPurpose purpose, CancellationToken cancellationToken);
    Task<bool> VerifyAsync(string destination, string otp, OtpPurpose purpose, CancellationToken cancellationToken);
}

public interface IEmailService
{
    Task SendOtpAsync(string recipient, string otp, CancellationToken cancellationToken);
    Task SendOrderConfirmationOtpAsync(string recipient, string otp, CartDto cart, CancellationToken cancellationToken);
    Task SendRegistrationSuccessAsync(string recipient, string name, string role, CancellationToken cancellationToken);
    Task SendProductQcResultAsync(string recipient, string productTitle, bool approved, string? notes, string? tags, CancellationToken cancellationToken);
    Task SendOrderPlacedAsync(string recipient, string orderNumber, IReadOnlyList<string> productLines, decimal total, CancellationToken cancellationToken);
    Task SendSellerOrderReceivedAsync(string recipient, string orderNumber, IReadOnlyList<string> productLines, decimal total, CancellationToken cancellationToken);
    Task SendOrderStatusChangedAsync(string recipient, string orderNumber, string status, CancellationToken cancellationToken);
    Task SendOrderCancelledAsync(string recipient, string orderNumber, CancellationToken cancellationToken);
}

public interface IProductService
{
    Task<ApiResponse<PagedResult<ProductDto>>> SearchAsync(ProductQuery query, CancellationToken cancellationToken);
    Task<ApiResponse<ProductDto>> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<ApiResponse<long>> CreateAsync(long sellerUserId, ProductUpsertRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<IReadOnlyList<ProductDto>>> GetSellerProductsAsync(long sellerUserId, CancellationToken cancellationToken);
    Task<ApiResponse<IReadOnlyList<ProductDto>>> GetProductsForQcAsync(CancellationToken cancellationToken);
    Task<ApiResponse<string>> UpdateAsync(long sellerUserId, long id, ProductUpsertRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<string>> SubmitForQcAsync(long sellerUserId, long id, CancellationToken cancellationToken);
    Task<ApiResponse<string>> ReviewQcAsync(long id, ProductQcReviewRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<string>> UpdateStockAsync(long sellerUserId, StockUpdateRequest request, CancellationToken cancellationToken);
}

public interface ICartService
{
    Task<ApiResponse<CartDto>> GetAsync(long userId, CancellationToken cancellationToken);
    Task<ApiResponse<CartDto>> AddOrUpdateAsync(long userId, CartItemRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<CartDto>> RemoveAsync(long userId, long itemId, CancellationToken cancellationToken);
}

public interface IOrderService
{
    Task<ApiResponse<string>> SendConfirmationOtpAsync(long userId, CancellationToken cancellationToken);
    Task<ApiResponse<OrderDto>> PlaceOrderAsync(long userId, CheckoutRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<IReadOnlyList<OrderDto>>> GetCustomerOrdersAsync(long userId, CancellationToken cancellationToken);
    Task<ApiResponse<IReadOnlyList<OrderDto>>> GetSellerOrdersAsync(long sellerUserId, CancellationToken cancellationToken);
    Task<ApiResponse<string>> UpdateStatusAsync(long sellerUserId, long orderId, OrderStatusUpdateRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<string>> CancelAsync(long userId, long orderId, CancellationToken cancellationToken);
}

public interface IAddressService
{
    Task<ApiResponse<IReadOnlyList<AddressDto>>> GetAsync(long userId, CancellationToken cancellationToken);
    Task<ApiResponse<AddressDto>> AddAsync(long userId, AddressRequest request, CancellationToken cancellationToken);
}

public interface ISellerDashboardService
{
    Task<ApiResponse<DashboardSummaryDto>> GetSummaryAsync(long sellerUserId, CancellationToken cancellationToken);
    Task<ApiResponse<IReadOnlyList<RevenuePointDto>>> GetRevenueAsync(long sellerUserId, CancellationToken cancellationToken);
}

public interface ISellerManagementService
{
    Task<ApiResponse<SellerOnboardingStatusDto>> GetOnboardingAsync(long sellerUserId, CancellationToken cancellationToken);
    Task<ApiResponse<string>> SubmitOnboardingAsync(long sellerUserId, SellerOnboardingRequest request, CancellationToken cancellationToken);
    Task<ApiResponse<AdminSellerSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken);
    Task<ApiResponse<IReadOnlyList<SellerReviewDto>>> GetSellersForReviewAsync(CancellationToken cancellationToken);
    Task<ApiResponse<string>> ReviewSellerAsync(long sellerId, SellerApprovalRequest request, CancellationToken cancellationToken);
}
