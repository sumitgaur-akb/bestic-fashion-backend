using System.Security.Claims;
using FlipShop.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected long CurrentUserId => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "0");

    protected static async Task<IActionResult> Wrap<T>(Task<ApiResponse<T>> task)
    {
        var response = await task;
        return response.Success ? new OkObjectResult(response) : new BadRequestObjectResult(response);
    }
}
