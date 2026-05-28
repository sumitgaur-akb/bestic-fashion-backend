using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

[Authorize(Roles = "Seller")]
public sealed class SellersController(ISellerManagementService sellerService) : ApiControllerBase
{
    [HttpPost("onboarding")]
    public async Task<IActionResult> SubmitOnboarding(SellerOnboardingRequest request, CancellationToken ct)
    {
        var response = await sellerService.SubmitOnboardingAsync(CurrentUserId, request, ct);
        return response.Success ? Ok(response) : BadRequest(response);
    }
}
