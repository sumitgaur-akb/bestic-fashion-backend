using FlipShop.Application.Common;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Domain.Enums;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace FlipShop.Infrastructure.Services;

public sealed class SellerManagementService(AppDbContext dbContext, IEmailService emailService) : ISellerManagementService
{
    public async Task<ApiResponse<string>> SubmitOnboardingAsync(long sellerUserId, SellerOnboardingRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BusinessName)) return ApiResponse<string>.Fail("Business name is required");
        if (string.IsNullOrWhiteSpace(request.PickupAddress)) return ApiResponse<string>.Fail("Pickup address is required");
        var validationErrors = ValidateKyc(request);
        if (validationErrors.Count > 0) return ApiResponse<string>.Fail("KYC validation failed", validationErrors);

        var seller = await dbContext.Sellers
            .Include(x => x.User)
            .Include(x => x.BusinessDetails)
            .Include(x => x.BankDetails)
            .FirstOrDefaultAsync(x => x.UserId == sellerUserId, cancellationToken);

        if (seller is null) return ApiResponse<string>.Fail("Seller profile not found");
        if (seller.Status == SellerStatus.Approved) return ApiResponse<string>.Fail("Seller is already approved");

        seller.Status = SellerStatus.PendingApproval;
        seller.BusinessDetails ??= new SellerBusinessDetails { SellerId = seller.Id };
        seller.BusinessDetails.BusinessName = request.BusinessName.Trim();
        seller.BusinessDetails.GstNumber = request.GstNumber;
        seller.BusinessDetails.PanNumber = request.PanNumber;
        seller.BusinessDetails.PickupAddress = request.PickupAddress;

        seller.BankDetails ??= new SellerBankDetails { SellerId = seller.Id };
        seller.BankDetails.BankName = request.BankName;
        seller.BankDetails.AccountHolderName = request.AccountHolderName;
        seller.BankDetails.AccountNumberMasked = request.AccountNumberMasked;
        seller.BankDetails.IfscCode = request.IfscCode;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<string>.Ok("KYC submitted. Admin approval is pending.");
    }

    private static List<string> ValidateKyc(SellerOnboardingRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.BusinessName) || request.BusinessName.Length < 3) errors.Add("Business name must be at least 3 characters.");
        if (!string.IsNullOrWhiteSpace(request.GstNumber) && !Regex.IsMatch(request.GstNumber, "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")) errors.Add("GST number format is invalid.");
        if (!string.IsNullOrWhiteSpace(request.PanNumber) && !Regex.IsMatch(request.PanNumber, "^[A-Z]{5}[0-9]{4}[A-Z]$")) errors.Add("PAN number format is invalid.");
        if (string.IsNullOrWhiteSpace(request.PickupAddress) || request.PickupAddress.Length < 10) errors.Add("Pickup address must be at least 10 characters.");
        if (string.IsNullOrWhiteSpace(request.AccountHolderName) || !Regex.IsMatch(request.AccountHolderName, "^[A-Za-z ]{3,}$")) errors.Add("Account holder name is invalid.");
        if (string.IsNullOrWhiteSpace(request.BankName)) errors.Add("Bank name is required.");
        if (string.IsNullOrWhiteSpace(request.AccountNumberMasked) || !Regex.IsMatch(request.AccountNumberMasked, "^[0-9]{9,18}$")) errors.Add("Account number must be 9 to 18 digits.");
        if (string.IsNullOrWhiteSpace(request.IfscCode) || !Regex.IsMatch(request.IfscCode, "^[A-Z]{4}0[A-Z0-9]{6}$")) errors.Add("IFSC code format is invalid.");
        return errors;
    }

    public async Task<ApiResponse<IReadOnlyList<SellerReviewDto>>> GetSellersForReviewAsync(CancellationToken cancellationToken)
    {
        var sellers = await dbContext.Sellers
            .Include(x => x.User)
            .Include(x => x.BusinessDetails)
            .Include(x => x.BankDetails)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return ApiResponse<IReadOnlyList<SellerReviewDto>>.Ok(sellers.Select(x => new SellerReviewDto(
            x.Id,
            x.UserId,
            x.User.FullName,
            x.User.Email,
            x.DisplayName,
            x.Status.ToString(),
            x.BusinessDetails?.BusinessName,
            x.BusinessDetails?.GstNumber,
            x.BusinessDetails?.PanNumber,
            x.BusinessDetails?.PickupAddress,
            x.BankDetails?.BankName,
            x.BankDetails?.AccountHolderName,
            x.CreatedAt,
            dbContext.Products.Count(p => p.SellerId == x.Id),
            dbContext.OrderItems.Count(i => i.SellerId == x.Id),
            dbContext.OrderItems.Where(i => i.SellerId == x.Id).Sum(i => i.LineTotal))).ToArray());
    }

    public async Task<ApiResponse<AdminSellerSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var sellers = await dbContext.Sellers.ToListAsync(cancellationToken);
        var revenue = await dbContext.OrderItems.SumAsync(x => x.LineTotal, cancellationToken);
        return ApiResponse<AdminSellerSummaryDto>.Ok(new AdminSellerSummaryDto(
            sellers.Count,
            sellers.Count(x => x.Status == SellerStatus.PendingOtp),
            sellers.Count(x => x.Status == SellerStatus.PendingKyc),
            sellers.Count(x => x.Status == SellerStatus.PendingApproval),
            sellers.Count(x => x.Status == SellerStatus.Approved),
            sellers.Count(x => x.Status == SellerStatus.Suspended),
            revenue));
    }

    public async Task<ApiResponse<string>> ReviewSellerAsync(long sellerId, SellerApprovalRequest request, CancellationToken cancellationToken)
    {
        var seller = await dbContext.Sellers.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null) return ApiResponse<string>.Fail("Seller not found");

        seller.Status = request.Approved ? SellerStatus.Approved : SellerStatus.Suspended;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.Approved)
        {
            await emailService.SendRegistrationSuccessAsync(seller.User.Email, seller.User.FullName, "Approved Seller", cancellationToken);
            return ApiResponse<string>.Ok("Seller approved successfully");
        }

        await emailService.SendOrderStatusChangedAsync(seller.User.Email, "Seller onboarding", "Rejected", cancellationToken);
        return ApiResponse<string>.Ok("Seller rejected");
    }
}
