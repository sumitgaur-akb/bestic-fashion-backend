using FlipShop.Domain.Enums;

namespace FlipShop.Application.DTOs;

public sealed record ProductQuery(string? Search, long? CategoryId, decimal? MinPrice, decimal? MaxPrice, int? MinRating, bool IncludeUnapproved = false, int Page = 1, int PageSize = 20, string? Sort = null);
public sealed record ProductImageDto(long Id, string ImageUrl, bool IsPrimary);
public sealed record ProductVariantDto(long Id, string Sku, string? Size, string? Color, decimal Price, int Stock);
public sealed record ProductDto(long Id, string Title, string Slug, string? Brand, decimal Price, decimal? DiscountPrice, decimal AverageRating, string SellerName, string ApprovalStatus, string? QcNotes, string? QcTags, string Description, IReadOnlyList<ProductImageDto> Images, IReadOnlyList<ProductVariantDto> Variants);
public sealed record ProductUpsertRequest(long CategoryId, long? SubCategoryId, string Title, string Description, string? Brand, decimal BasePrice, decimal? DiscountPrice, string? ImageUrl, IReadOnlyList<ProductVariantUpsertRequest> Variants);
public sealed record ProductVariantUpsertRequest(string Sku, string? Size, string? Color, decimal Price, int Stock, int LowStockThreshold);
public sealed record StockUpdateRequest(long VariantId, int Quantity);
public sealed record ProductApprovalRequest(ProductApprovalStatus Status, string? Notes);
public sealed record ProductQcReviewRequest(bool Approved, string? Notes, IReadOnlyList<string>? Tags);
