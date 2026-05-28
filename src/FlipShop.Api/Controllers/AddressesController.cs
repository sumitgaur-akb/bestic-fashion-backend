using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

[Authorize(Roles = "Customer")]
public sealed class AddressesController(IAddressService addressService) : ApiControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct) => Wrap(addressService.GetAsync(CurrentUserId, ct));

    [HttpPost]
    public Task<IActionResult> Add(AddressRequest request, CancellationToken ct) => Wrap(addressService.AddAsync(CurrentUserId, request, ct));
}
