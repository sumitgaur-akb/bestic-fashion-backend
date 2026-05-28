using FlipShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

[Authorize(Roles = "Seller")]
public sealed class SellerDashboardController(ISellerDashboardService dashboardService) : ApiControllerBase
{
    [HttpGet("summary")]
    public Task<IActionResult> Summary(CancellationToken ct) => Wrap(dashboardService.GetSummaryAsync(CurrentUserId, ct));

    [HttpGet("revenue")]
    public Task<IActionResult> Revenue(CancellationToken ct) => Wrap(dashboardService.GetRevenueAsync(CurrentUserId, ct));

    [HttpGet("recent-orders")]
    public IActionResult RecentOrders() => Ok(new { items = Array.Empty<object>() });

    [HttpGet("low-stock")]
    public IActionResult LowStockProducts() => Ok(new { items = Array.Empty<object>() });

    [HttpGet("product-performance")]
    public IActionResult ProductPerformance() => Ok(new { items = Array.Empty<object>() });
}
