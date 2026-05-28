using FlipShop.Application.Common;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Enums;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlipShop.Infrastructure.Services;

public sealed class SellerDashboardService(AppDbContext dbContext) : ISellerDashboardService
{
    public async Task<ApiResponse<DashboardSummaryDto>> GetSummaryAsync(long sellerUserId, CancellationToken cancellationToken)
    {
        var seller = await dbContext.Sellers.FirstOrDefaultAsync(x => x.UserId == sellerUserId, cancellationToken);
        if (seller is null) return ApiResponse<DashboardSummaryDto>.Fail("Seller not found");
        if (seller.Status != SellerStatus.Approved) return ApiResponse<DashboardSummaryDto>.Fail("Seller approval is required");
        var items = dbContext.OrderItems.Where(x => x.SellerId == seller.Id);
        var today = DateTime.UtcNow.Date;
        var summary = new DashboardSummaryDto(
            await items.CountAsync(cancellationToken),
            await items.CountAsync(x => x.SellerOrderStatus == OrderStatus.Placed || x.SellerOrderStatus == OrderStatus.Confirmed, cancellationToken),
            await items.CountAsync(x => x.SellerOrderStatus == OrderStatus.Delivered, cancellationToken),
            await items.CountAsync(x => x.SellerOrderStatus == OrderStatus.Cancelled, cancellationToken),
            await items.SumAsync(x => x.LineTotal, cancellationToken),
            await items.Where(x => x.CreatedAt >= today).SumAsync(x => x.LineTotal, cancellationToken),
            await dbContext.Products.CountAsync(x => x.SellerId == seller.Id, cancellationToken),
            await dbContext.ProductStock.CountAsync(x => x.ProductVariant.Product.SellerId == seller.Id && x.Quantity <= x.LowStockThreshold, cancellationToken));
        return ApiResponse<DashboardSummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<IReadOnlyList<RevenuePointDto>>> GetRevenueAsync(long sellerUserId, CancellationToken cancellationToken)
    {
        var seller = await dbContext.Sellers.FirstOrDefaultAsync(x => x.UserId == sellerUserId, cancellationToken);
        if (seller is null) return ApiResponse<IReadOnlyList<RevenuePointDto>>.Fail("Seller not found");
        if (seller.Status != SellerStatus.Approved) return ApiResponse<IReadOnlyList<RevenuePointDto>>.Fail("Seller approval is required");
        var from = DateTime.UtcNow.Date.AddDays(-13);
        var points = await dbContext.OrderItems.Where(x => x.SellerId == seller.Id && x.CreatedAt >= from)
            .GroupBy(x => x.CreatedAt.Date)
            .Select(g => new RevenuePointDto(DateOnly.FromDateTime(g.Key), g.Sum(x => x.LineTotal)))
            .ToListAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<RevenuePointDto>>.Ok(points);
    }
}
