using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

[Authorize(Roles = "Customer")]
public sealed class WishlistController : ApiControllerBase
{
    [HttpGet]
    public IActionResult GetWishlist() => Ok(new { items = Array.Empty<object>() });

    [HttpPost("{productId:long}")]
    public IActionResult Add(long productId) => Accepted(new { productId, message = "Wishlist add placeholder" });

    [HttpDelete("{productId:long}")]
    public IActionResult Remove(long productId) => Accepted(new { productId, message = "Wishlist remove placeholder" });
}
