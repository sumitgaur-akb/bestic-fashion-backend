using FlipShop.Application.Common;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlipShop.Infrastructure.Services;

public sealed class ProductService(AppDbContext dbContext, IEmailService emailService) : IProductService
{
    public async Task<ApiResponse<PagedResult<ProductDto>>> SearchAsync(ProductQuery query, CancellationToken cancellationToken)
    {
        var products = ProductQueryBase();
        if (!query.IncludeUnapproved) products = products.Where(x => x.ApprovalStatus == FlipShop.Domain.Enums.ProductApprovalStatus.Approved);
        if (!string.IsNullOrWhiteSpace(query.Search)) products = products.Where(x => x.Title.Contains(query.Search) || (x.Brand != null && x.Brand.Contains(query.Search)));
        if (query.CategoryId.HasValue) products = products.Where(x => x.CategoryId == query.CategoryId);
        if (query.MinPrice.HasValue) products = products.Where(x => x.BasePrice >= query.MinPrice);
        if (query.MaxPrice.HasValue) products = products.Where(x => x.BasePrice <= query.MaxPrice);
        if (query.MinRating.HasValue) products = products.Where(x => x.AverageRating >= query.MinRating);
        products = query.Sort == "price_desc" ? products.OrderByDescending(x => x.BasePrice) : query.Sort == "price_asc" ? products.OrderBy(x => x.BasePrice) : products.OrderByDescending(x => x.CreatedAt);
        var total = await products.LongCountAsync(cancellationToken);
        var entities = await products.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync(cancellationToken);
        var items = entities.Select(ToDto).ToArray();
        return ApiResponse<PagedResult<ProductDto>>.Ok(new PagedResult<ProductDto>(items, query.Page, query.PageSize, total));
    }

    public async Task<ApiResponse<ProductDto>> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var product = await ProductQueryBase().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return product is null ? ApiResponse<ProductDto>.Fail("Product not found") : ApiResponse<ProductDto>.Ok(ToDto(product));
    }

    public async Task<ApiResponse<long>> CreateAsync(long sellerUserId, ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        var seller = await dbContext.Sellers.FirstOrDefaultAsync(x => x.UserId == sellerUserId, cancellationToken);
        if (seller is null) return ApiResponse<long>.Fail("Seller not found");
        if (seller.Status != FlipShop.Domain.Enums.SellerStatus.Approved) return ApiResponse<long>.Fail("Seller approval is required before adding products");
        var product = new Product { SellerId = seller.Id, CategoryId = request.CategoryId, SubCategoryId = request.SubCategoryId, Title = request.Title, Slug = Slug(request.Title), Description = request.Description, Brand = request.Brand, BasePrice = request.BasePrice, DiscountPrice = request.DiscountPrice, ApprovalStatus = FlipShop.Domain.Enums.ProductApprovalStatus.PendingApproval };
        product.Variants = request.Variants.Select(v => new ProductVariant { Sku = v.Sku, Size = v.Size, Color = v.Color, Price = v.Price, Stock = new ProductStock { Quantity = v.Stock, LowStockThreshold = v.LowStockThreshold } }).ToList();
        product.Images = [new ProductImage { ImageUrl = string.IsNullOrWhiteSpace(request.ImageUrl) ? "/assets/demo-phone.jpg" : request.ImageUrl, IsPrimary = true, AltText = request.Title }];
        await dbContext.Products.AddAsync(product, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<long>.Ok(product.Id, "Product sent to QC");
    }

    public async Task<ApiResponse<IReadOnlyList<ProductDto>>> GetSellerProductsAsync(long sellerUserId, CancellationToken cancellationToken)
    {
        var seller = await dbContext.Sellers.FirstOrDefaultAsync(x => x.UserId == sellerUserId, cancellationToken);
        if (seller is null) return ApiResponse<IReadOnlyList<ProductDto>>.Fail("Seller not found");
        var products = await ProductQueryBase().Where(x => x.SellerId == seller.Id).OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<ProductDto>>.Ok(products.Select(ToDto).ToArray());
    }

    public async Task<ApiResponse<IReadOnlyList<ProductDto>>> GetProductsForQcAsync(CancellationToken cancellationToken)
    {
        var products = await ProductQueryBase().OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<ProductDto>>.Ok(products.Select(ToDto).ToArray());
    }

    public async Task<ApiResponse<string>> UpdateAsync(long sellerUserId, long id, ProductUpsertRequest request, CancellationToken cancellationToken)
    {
        var product = await ProductQueryBase().FirstOrDefaultAsync(x => x.Id == id && x.Seller.UserId == sellerUserId, cancellationToken);
        if (product is null) return ApiResponse<string>.Fail("Product not found");
        if (product.ApprovalStatus == FlipShop.Domain.Enums.ProductApprovalStatus.Approved) return ApiResponse<string>.Fail("Approved product edits are not enabled yet");
        product.Title = request.Title;
        product.Description = request.Description;
        product.Brand = request.Brand;
        product.BasePrice = request.BasePrice;
        product.DiscountPrice = request.DiscountPrice;
        product.CategoryId = request.CategoryId;
        product.SubCategoryId = request.SubCategoryId;
        product.QcNotes = null;
        product.QcTags = null;
        product.ApprovalStatus = FlipShop.Domain.Enums.ProductApprovalStatus.PendingApproval;
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            var primary = product.Images.FirstOrDefault(x => x.IsPrimary) ?? new ProductImage { ProductId = product.Id, IsPrimary = true, AltText = request.Title };
            primary.ImageUrl = request.ImageUrl;
            if (primary.Id == 0) product.Images.Add(primary);
        }
        var variant = product.Variants.FirstOrDefault();
        var requested = request.Variants.FirstOrDefault();
        if (variant is not null && requested is not null)
        {
            variant.Sku = requested.Sku;
            variant.Size = requested.Size;
            variant.Color = requested.Color;
            variant.Price = requested.Price;
            variant.Stock ??= new ProductStock { ProductVariantId = variant.Id };
            variant.Stock.Quantity = requested.Stock;
            variant.Stock.LowStockThreshold = requested.LowStockThreshold;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<string>.Ok("Product corrected and sent back to QC");
    }

    public async Task<ApiResponse<string>> SubmitForQcAsync(long sellerUserId, long id, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.Include(x => x.Seller).FirstOrDefaultAsync(x => x.Id == id && x.Seller.UserId == sellerUserId, cancellationToken);
        if (product is null) return ApiResponse<string>.Fail("Product not found");
        product.ApprovalStatus = FlipShop.Domain.Enums.ProductApprovalStatus.PendingApproval;
        product.QcNotes = null;
        product.QcTags = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<string>.Ok("Product sent to QC");
    }

    public async Task<ApiResponse<string>> ReviewQcAsync(long id, ProductQcReviewRequest request, CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.Include(x => x.Seller).ThenInclude(x => x.User).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (product is null) return ApiResponse<string>.Fail("Product not found");
        product.ApprovalStatus = request.Approved ? FlipShop.Domain.Enums.ProductApprovalStatus.Approved : FlipShop.Domain.Enums.ProductApprovalStatus.Rejected;
        product.QcNotes = request.Notes;
        product.QcTags = request.Tags is { Count: > 0 } ? string.Join(", ", request.Tags) : null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await emailService.SendProductQcResultAsync(product.Seller.User.Email, product.Title, request.Approved, product.QcNotes, product.QcTags, cancellationToken);
        return ApiResponse<string>.Ok(request.Approved ? "Product approved and listed" : "Product marked QC failed");
    }

    public async Task<ApiResponse<string>> UpdateStockAsync(long sellerUserId, StockUpdateRequest request, CancellationToken cancellationToken)
    {
        var stock = await dbContext.ProductStock.Include(x => x.ProductVariant).ThenInclude(x => x.Product).ThenInclude(x => x.Seller).FirstOrDefaultAsync(x => x.ProductVariantId == request.VariantId, cancellationToken);
        if (stock is null || stock.ProductVariant.Product.Seller.UserId != sellerUserId) return ApiResponse<string>.Fail("Stock item not found");
        if (stock.ProductVariant.Product.Seller.Status != FlipShop.Domain.Enums.SellerStatus.Approved) return ApiResponse<string>.Fail("Seller approval is required before updating stock");
        stock.Quantity = request.Quantity;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<string>.Ok("Stock updated");
    }

    private IQueryable<Product> ProductQueryBase() => dbContext.Products.Include(x => x.Seller).ThenInclude(x => x.User).Include(x => x.Images).Include(x => x.Variants).ThenInclude(x => x.Stock).Where(x => x.IsActive);
    private static ProductDto ToDto(Product x) => new(x.Id, x.Title, x.Slug, x.Brand, x.BasePrice, x.DiscountPrice, x.AverageRating, x.Seller.DisplayName, x.ApprovalStatus.ToString(), x.QcNotes, x.QcTags, x.Description, x.Images.Select(i => new ProductImageDto(i.Id, i.ImageUrl, i.IsPrimary)).ToArray(), x.Variants.Select(v => new ProductVariantDto(v.Id, v.Sku, v.Size, v.Color, v.Price, v.Stock?.Quantity ?? 0)).ToArray());
    private static string Slug(string value) => $"{value.ToLowerInvariant().Replace(' ', '-')}-{Guid.NewGuid():N}"[..Math.Min(value.Length + 33, 240)];
}
