using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

[Authorize(Roles = "Customer")]
public sealed class CartController(ICartService cartService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await cartService.GetAsync(CurrentUserId, ct));

    [HttpPost]
    public async Task<IActionResult> Add(CartItemRequest request, CancellationToken ct) => Ok(await cartService.AddOrUpdateAsync(CurrentUserId, request, ct));

    [HttpPut]
    public async Task<IActionResult> Update(CartItemRequest request, CancellationToken ct) => Ok(await cartService.AddOrUpdateAsync(CurrentUserId, request, ct));

    [HttpDelete("{itemId:long}")]
    public async Task<IActionResult> Remove(long itemId, CancellationToken ct) => Ok(await cartService.RemoveAsync(CurrentUserId, itemId, ct));
}
