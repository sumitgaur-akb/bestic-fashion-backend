using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

[Authorize]
public sealed class OrdersController(IOrderService orderService) : ApiControllerBase
{
    [Authorize(Roles = "Customer")]
    [HttpPost("confirmation-otp")]
    public Task<IActionResult> ConfirmationOtp(CancellationToken ct) => Wrap(orderService.SendConfirmationOtpAsync(CurrentUserId, ct));

    [Authorize(Roles = "Customer")]
    [HttpPost]
    public Task<IActionResult> Place(CheckoutRequest request, CancellationToken ct) => Wrap(orderService.PlaceOrderAsync(CurrentUserId, request, ct));

    [Authorize(Roles = "Customer")]
    [HttpGet("customer")]
    public Task<IActionResult> CustomerOrders(CancellationToken ct) => Wrap(orderService.GetCustomerOrdersAsync(CurrentUserId, ct));

    [Authorize(Roles = "Seller")]
    [HttpGet("seller")]
    public Task<IActionResult> SellerOrders(CancellationToken ct) => Wrap(orderService.GetSellerOrdersAsync(CurrentUserId, ct));

    [Authorize(Roles = "Seller")]
    [HttpPut("{orderId:long}/status")]
    public Task<IActionResult> UpdateStatus(long orderId, OrderStatusUpdateRequest request, CancellationToken ct) => Wrap(orderService.UpdateStatusAsync(CurrentUserId, orderId, request, ct));

    [Authorize(Roles = "Customer")]
    [HttpPost("{orderId:long}/cancel")]
    public Task<IActionResult> Cancel(long orderId, CancellationToken ct) => Wrap(orderService.CancelAsync(CurrentUserId, orderId, ct));

    [HttpGet("{orderId:long}")]
    public IActionResult Details(long orderId) => Ok(new { orderId, message = "Order detail placeholder with items, history and invoice link" });
}
