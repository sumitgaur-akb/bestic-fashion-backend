using FlipShop.Application.Common;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlipShop.Infrastructure.Services;

public sealed class CartService(AppDbContext dbContext) : ICartService
{
    public async Task<ApiResponse<CartDto>> GetAsync(long userId, CancellationToken cancellationToken)
    {
        var cart = await LoadCart(userId, cancellationToken) ?? new Cart { UserId = userId };
        return ApiResponse<CartDto>.Ok(ToDto(cart));
    }

    public async Task<ApiResponse<CartDto>> AddOrUpdateAsync(long userId, CartItemRequest request, CancellationToken cancellationToken)
    {
        var variant = await dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == request.ProductVariantId, cancellationToken);
        if (variant is null) return ApiResponse<CartDto>.Fail("Variant not found");
        var cart = await LoadCart(userId, cancellationToken);
        if (cart is null)
        {
            cart = new Cart { UserId = userId };
            await dbContext.Carts.AddAsync(cart, cancellationToken);
        }
        var item = cart.Items.FirstOrDefault(x => x.ProductVariantId == request.ProductVariantId);
        if (item is null) cart.Items.Add(new CartItem { ProductVariantId = request.ProductVariantId, Quantity = request.Quantity, UnitPrice = variant.Price });
        else item.Quantity = request.Quantity;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<CartDto>.Ok(ToDto(await LoadCart(userId, cancellationToken) ?? cart));
    }

    public async Task<ApiResponse<CartDto>> RemoveAsync(long userId, long itemId, CancellationToken cancellationToken)
    {
        var cart = await LoadCart(userId, cancellationToken);
        var item = cart?.Items.FirstOrDefault(x => x.Id == itemId);
        if (item is not null) dbContext.CartItems.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<CartDto>.Ok(ToDto(await LoadCart(userId, cancellationToken) ?? new Cart { UserId = userId }));
    }

    private Task<Cart?> LoadCart(long userId, CancellationToken ct) => dbContext.Carts.Include(x => x.Items).ThenInclude(x => x.ProductVariant).ThenInclude(x => x.Product).FirstOrDefaultAsync(x => x.UserId == userId, ct);
    private static CartDto ToDto(Cart cart)
    {
        var items = cart.Items.Select(x => new CartItemDto(x.Id, x.ProductVariantId, x.ProductVariant?.Product?.Title ?? "Product", x.Quantity, x.UnitPrice, x.Quantity * x.UnitPrice)).ToArray();
        return new CartDto(cart.Id, items, items.Sum(x => x.LineTotal));
    }
}
