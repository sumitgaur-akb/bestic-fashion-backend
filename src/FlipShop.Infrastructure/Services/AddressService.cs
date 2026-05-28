using FlipShop.Application.Common;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlipShop.Infrastructure.Services;

public sealed class AddressService(AppDbContext dbContext) : IAddressService
{
    public async Task<ApiResponse<IReadOnlyList<AddressDto>>> GetAsync(long userId, CancellationToken cancellationToken)
    {
        var addresses = await dbContext.Set<Address>().Where(x => x.UserId == userId).OrderByDescending(x => x.IsDefault).ThenByDescending(x => x.Id).ToListAsync(cancellationToken);
        return ApiResponse<IReadOnlyList<AddressDto>>.Ok(addresses.Select(ToDto).ToArray());
    }

    public async Task<ApiResponse<AddressDto>> AddAsync(long userId, AddressRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Phone) || string.IsNullOrWhiteSpace(request.Line1) || string.IsNullOrWhiteSpace(request.City) || string.IsNullOrWhiteSpace(request.State) || string.IsNullOrWhiteSpace(request.PostalCode))
            return ApiResponse<AddressDto>.Fail("Complete delivery address is required");

        if (request.IsDefault)
        {
            var existing = await dbContext.Set<Address>().Where(x => x.UserId == userId).ToListAsync(cancellationToken);
            foreach (var address in existing) address.IsDefault = false;
        }

        var entity = new Address
        {
            UserId = userId,
            FullName = request.FullName.Trim(),
            Phone = request.Phone.Trim(),
            Line1 = request.Line1.Trim(),
            Line2 = request.Line2,
            City = request.City.Trim(),
            State = request.State.Trim(),
            PostalCode = request.PostalCode.Trim(),
            IsDefault = request.IsDefault
        };
        await dbContext.Set<Address>().AddAsync(entity, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<AddressDto>.Ok(ToDto(entity), "Address saved");
    }

    private static AddressDto ToDto(Address x) => new(x.Id, x.FullName, x.Phone, x.Line1, x.Line2, x.City, x.State, x.PostalCode, x.IsDefault);
}
