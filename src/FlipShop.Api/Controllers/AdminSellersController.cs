using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

[Authorize(Roles = "Admin")]
public sealed class AdminSellersController(ISellerManagementService sellerService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSellers(CancellationToken ct) => Ok(await sellerService.GetSellersForReviewAsync(ct));

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct) => Ok(await sellerService.GetSummaryAsync(ct));

    [HttpPost("{sellerId:long}/review")]
    public async Task<IActionResult> Review(long sellerId, SellerApprovalRequest request, CancellationToken ct)
    {
        var response = await sellerService.ReviewSellerAsync(sellerId, request, ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
