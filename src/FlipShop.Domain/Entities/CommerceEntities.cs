using FlipShop.Domain.Common;
using FlipShop.Domain.Enums;

namespace FlipShop.Domain.Entities;

public sealed class User : BaseEntity
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Mobile { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public bool EmailVerified { get; set; }
    public bool MobileVerified { get; set; }
    public ICollection<UserRole> UserRoles { get; set; } = [];
    public Seller? Seller { get; set; }
}

public sealed class Role : BaseEntity { public string Name { get; set; } = string.Empty; public ICollection<UserRole> UserRoles { get; set; } = []; }

public sealed class UserRole : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long RoleId { get; set; }
    public Role Role { get; set; } = null!;
}

public sealed class Seller : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string DisplayName { get; set; } = string.Empty;
    public SellerStatus Status { get; set; } = SellerStatus.PendingOtp;
    public decimal Rating { get; set; }
    public SellerBusinessDetails? BusinessDetails { get; set; }
    public SellerBankDetails? BankDetails { get; set; }
    public ICollection<Product> Products { get; set; } = [];
}

public sealed class SellerBusinessDetails : BaseEntity
{
    public long SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    public string BusinessName { get; set; } = string.Empty;
    public string? GstNumber { get; set; }
    public string? PanNumber { get; set; }
    public string? PickupAddress { get; set; }
}

public sealed class SellerBankDetails : BaseEntity
{
    public long SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    public string? AccountHolderName { get; set; }
    public string? BankName { get; set; }
    public string? AccountNumberMasked { get; set; }
    public string? IfscCode { get; set; }
}

public sealed class Category : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public ICollection<SubCategory> SubCategories { get; set; } = [];
}

public sealed class SubCategory : BaseEntity
{
    public long CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}

public sealed class Product : BaseEntity
{
    public long SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    public long CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public long? SubCategoryId { get; set; }
    public SubCategory? SubCategory { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public decimal BasePrice { get; set; }
    public decimal? DiscountPrice { get; set; }
    public ProductApprovalStatus ApprovalStatus { get; set; } = ProductApprovalStatus.PendingApproval;
    public string? QcNotes { get; set; }
    public string? QcTags { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public ICollection<ProductImage> Images { get; set; } = [];
    public ICollection<ProductVariant> Variants { get; set; } = [];
}

public sealed class ProductImage : BaseEntity
{
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string ImageUrl { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public int SortOrder { get; set; }
    public bool IsPrimary { get; set; }
}

public sealed class ProductVariant : BaseEntity
{
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string Sku { get; set; } = string.Empty;
    public string? Size { get; set; }
    public string? Color { get; set; }
    public decimal Price { get; set; }
    public ProductStock? Stock { get; set; }
}

public sealed class ProductStock : BaseEntity
{
    public long ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int Quantity { get; set; }
    public int LowStockThreshold { get; set; } = 5;
}

public sealed class Cart : BaseEntity { public long UserId { get; set; } public User User { get; set; } = null!; public ICollection<CartItem> Items { get; set; } = []; }

public sealed class CartItem : BaseEntity
{
    public long CartId { get; set; }
    public Cart Cart { get; set; } = null!;
    public long ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class Wishlist : BaseEntity { public long UserId { get; set; } public User User { get; set; } = null!; public ICollection<WishlistItem> Items { get; set; } = []; }

public sealed class WishlistItem : BaseEntity
{
    public long WishlistId { get; set; }
    public Wishlist Wishlist { get; set; } = null!;
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;
}

public sealed class Address : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = "India";
    public bool IsDefault { get; set; }
}

public sealed class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long AddressId { get; set; }
    public Address Address { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public OrderStatus OrderStatus { get; set; } = OrderStatus.Placed;
    public ICollection<OrderItem> Items { get; set; } = [];
}

public sealed class OrderItem : BaseEntity
{
    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public long SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public long ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public OrderStatus SellerOrderStatus { get; set; } = OrderStatus.Placed;
}

public sealed class OrderStatusHistory : BaseEntity
{
    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public long? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public sealed class Payment : BaseEntity
{
    public long OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public string? Provider { get; set; }
    public string? ProviderReference { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
}

public sealed class OtpVerification : BaseEntity
{
    public long? UserId { get; set; }
    public User? User { get; set; }
    public OtpPurpose Purpose { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string OtpHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int ResendCount { get; set; }
}

public sealed class EmailLog : BaseEntity
{
    public string Recipient { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TemplateKey { get; set; } = string.Empty;
    public EmailStatus Status { get; set; } = EmailStatus.Queued;
    public string? ErrorMessage { get; set; }
}

public sealed class Notification : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
}

public sealed class Review : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public long? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
}

public sealed class Rating : BaseEntity
{
    public long UserId { get; set; }
    public User User { get; set; } = null!;
    public long ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public long? ReviewId { get; set; }
    public Review? Review { get; set; }
    public int Value { get; set; }
}
