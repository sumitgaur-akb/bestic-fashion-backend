using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FlipShop.Api.Controllers;

public sealed class ProductsController(IProductService productService) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProducts([FromQuery] ProductQuery query, CancellationToken ct) => Ok(await productService.SearchAsync(query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetProduct(long id, CancellationToken ct) => Ok(await productService.GetByIdAsync(id, ct));

    [Authorize(Roles = "Seller")]
    [HttpPost]
    public Task<IActionResult> AddProduct(ProductUpsertRequest request, CancellationToken ct) => Wrap(productService.CreateAsync(CurrentUserId, request, ct));

    [Authorize(Roles = "Seller")]
    [HttpGet("seller")]
    public Task<IActionResult> SellerProducts(CancellationToken ct) => Wrap(productService.GetSellerProductsAsync(CurrentUserId, ct));

    [Authorize(Roles = "Admin")]
    [HttpGet("qc")]
    public Task<IActionResult> ProductsForQc(CancellationToken ct) => Wrap(productService.GetProductsForQcAsync(ct));

    [Authorize(Roles = "Admin")]
    [HttpPost("{id:long}/qc")]
    public Task<IActionResult> ReviewQc(long id, ProductQcReviewRequest request, CancellationToken ct) => Wrap(productService.ReviewQcAsync(id, request, ct));

    [Authorize(Roles = "Seller")]
    [HttpPost("{id:long}/submit-qc")]
    public Task<IActionResult> SubmitQc(long id, CancellationToken ct) => Wrap(productService.SubmitForQcAsync(CurrentUserId, id, ct));

    [Authorize(Roles = "Seller")]
    [HttpPut("stock")]
    public Task<IActionResult> UpdateStock(StockUpdateRequest request, CancellationToken ct) => Wrap(productService.UpdateStockAsync(CurrentUserId, request, ct));

    [Authorize(Roles = "Seller")]
    [HttpPost("{id:long}/images")]
    public IActionResult UploadProductImages(long id, IFormFileCollection files) => Accepted(new { productId = id, fileCount = files.Count, message = "Image upload placeholder: wire to S3/Azure/local storage" });

    [Authorize(Roles = "Seller")]
    [HttpPut("{id:long}")]
    public Task<IActionResult> UpdateProduct(long id, ProductUpsertRequest request, CancellationToken ct) => Wrap(productService.UpdateAsync(CurrentUserId, id, request, ct));

    [Authorize(Roles = "Seller")]
    [HttpDelete("{id:long}")]
    public IActionResult DeleteProduct(long id) => Accepted(new { productId = id, message = "Soft delete placeholder" });
}
