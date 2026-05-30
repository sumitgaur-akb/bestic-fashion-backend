using System.Text.RegularExpressions;
using FlipShop.Application.Common;
using FlipShop.Application.DTOs;
using FlipShop.Application.Interfaces;
using FlipShop.Domain.Entities;
using FlipShop.Domain.Enums;
using FlipShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlipShop.Infrastructure.Services;

public sealed class SellerManagementService(AppDbContext dbContext, IEmailService emailService) : ISellerManagementService
{
    public async Task<ApiResponse<SellerOnboardingStatusDto>> GetOnboardingAsync(long sellerUserId, CancellationToken cancellationToken)
    {
        var seller = await LoadSellerAsync(sellerUserId, cancellationToken);
        if (seller is null) return ApiResponse<SellerOnboardingStatusDto>.Fail("Seller profile not found");
        return ApiResponse<SellerOnboardingStatusDto>.Ok(ToOnboardingDto(seller));
    }

    public async Task<ApiResponse<string>> SubmitOnboardingAsync(long sellerUserId, SellerOnboardingRequest request, CancellationToken cancellationToken)
    {
        var seller = await LoadSellerAsync(sellerUserId, cancellationToken);
        if (seller is null) return ApiResponse<string>.Fail("Seller profile not found");
        if (seller.Status == SellerStatus.Approved) return ApiResponse<string>.Fail("Seller is already approved");

        var validationErrors = ValidateOnboarding(request);
        if (validationErrors.Count > 0) return ApiResponse<string>.Fail("Seller onboarding validation failed", validationErrors);

        seller.Status = SellerStatus.UnderReview;
        seller.SubmittedAt = DateTime.UtcNow;
        seller.ReviewNotes = null;
        seller.BusinessDetails ??= new SellerBusinessDetails { SellerId = seller.Id };
        seller.BusinessDetails.BusinessName = request.Business.BusinessName.Trim();
        seller.BusinessDetails.LegalBusinessName = request.Business.LegalBusinessName.Trim();
        seller.BusinessDetails.BusinessType = request.Business.BusinessType;
        seller.BusinessDetails.GstNumber = Clean(request.Business.GstNumber);
        seller.BusinessDetails.PanNumber = Clean(request.Business.PanNumber);
        seller.BusinessDetails.CinNumber = Clean(request.Business.CinNumber);
        seller.BusinessDetails.BusinessAddress = request.Business.BusinessAddress.Trim();
        seller.BusinessDetails.Pincode = request.Business.Pincode.Trim();
        seller.BusinessDetails.PickupAddress = request.Business.BusinessAddress.Trim();

        seller.BankDetails ??= new SellerBankDetails { SellerId = seller.Id };
        seller.BankDetails.AccountHolderName = request.Bank.AccountHolderName.Trim();
        seller.BankDetails.BankName = request.Bank.BankName.Trim();
        seller.BankDetails.AccountNumberMasked = MaskAccount(request.Bank.AccountNumber);
        seller.BankDetails.AccountNumberLast4 = Last4(request.Bank.AccountNumber);
        seller.BankDetails.IfscCode = request.Bank.IfscCode.Trim().ToUpperInvariant();

        ReplaceWarehouses(seller, request.Warehouses);
        UpsertDocuments(seller, request.Documents);

        await dbContext.SaveChangesAsync(cancellationToken);
        return ApiResponse<string>.Ok("Seller onboarding submitted. Admin verification is now pending.");
    }

    public async Task<ApiResponse<IReadOnlyList<SellerReviewDto>>> GetSellersForReviewAsync(CancellationToken cancellationToken)
    {
        var sellers = await dbContext.Sellers
            .Include(x => x.User)
            .Include(x => x.BusinessDetails)
            .Include(x => x.BankDetails)
            .Include(x => x.Documents)
            .Include(x => x.Warehouses)
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
            x.BusinessDetails?.LegalBusinessName,
            x.BusinessDetails?.BusinessType.ToString(),
            x.BusinessDetails?.GstNumber,
            x.BusinessDetails?.PanNumber,
            x.BusinessDetails?.PickupAddress,
            x.BankDetails?.BankName,
            x.BankDetails?.AccountHolderName,
            x.CreatedAt,
            dbContext.Products.Count(p => p.SellerId == x.Id),
            dbContext.OrderItems.Count(i => i.SellerId == x.Id),
            dbContext.OrderItems.Where(i => i.SellerId == x.Id).Sum(i => i.LineTotal),
            x.Documents.Count,
            x.Warehouses.Count)).ToArray());
    }

    public async Task<ApiResponse<AdminSellerSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var sellers = await dbContext.Sellers.ToListAsync(cancellationToken);
        var revenue = await dbContext.OrderItems.SumAsync(x => x.LineTotal, cancellationToken);
        return ApiResponse<AdminSellerSummaryDto>.Ok(new AdminSellerSummaryDto(
            sellers.Count,
            sellers.Count(x => x.Status == SellerStatus.PendingOtp),
            sellers.Count(x => x.Status == SellerStatus.PendingKyc || x.Status == SellerStatus.Draft),
            sellers.Count(x => x.Status == SellerStatus.PendingApproval || x.Status == SellerStatus.PendingVerification || x.Status == SellerStatus.UnderReview),
            sellers.Count(x => x.Status == SellerStatus.Approved),
            sellers.Count(x => x.Status == SellerStatus.Rejected || x.Status == SellerStatus.Suspended),
            revenue));
    }

    public async Task<ApiResponse<string>> ReviewSellerAsync(long sellerId, SellerApprovalRequest request, CancellationToken cancellationToken)
    {
        var seller = await dbContext.Sellers.Include(x => x.User).FirstOrDefaultAsync(x => x.Id == sellerId, cancellationToken);
        if (seller is null) return ApiResponse<string>.Fail("Seller not found");

        seller.ReviewedAt = DateTime.UtcNow;
        seller.ReviewNotes = request.Notes;

        if (request.RequestAdditionalDocuments)
        {
            seller.Status = SellerStatus.PendingVerification;
            await dbContext.SaveChangesAsync(cancellationToken);
            await emailService.SendOrderStatusChangedAsync(seller.User.Email, "Seller onboarding", "Additional documents requested", cancellationToken);
            return ApiResponse<string>.Ok("Additional documents requested from seller");
        }

        seller.Status = request.Approved ? SellerStatus.Approved : SellerStatus.Rejected;
        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.Approved)
        {
            await emailService.SendRegistrationSuccessAsync(seller.User.Email, seller.User.FullName, "Approved Seller", cancellationToken);
            return ApiResponse<string>.Ok("Seller approved successfully");
        }

        await emailService.SendOrderStatusChangedAsync(seller.User.Email, "Seller onboarding", "Rejected", cancellationToken);
        return ApiResponse<string>.Ok("Seller rejected");
    }

    private Task<Seller?> LoadSellerAsync(long sellerUserId, CancellationToken cancellationToken) => dbContext.Sellers
        .Include(x => x.BusinessDetails)
        .Include(x => x.BankDetails)
        .Include(x => x.Warehouses)
        .Include(x => x.Documents)
        .FirstOrDefaultAsync(x => x.UserId == sellerUserId, cancellationToken);

    private static SellerOnboardingStatusDto ToOnboardingDto(Seller seller)
    {
        var business = seller.BusinessDetails is null
            ? null
            : new SellerBusinessInfoRequest(
                seller.BusinessDetails.BusinessName,
                seller.BusinessDetails.LegalBusinessName,
                seller.BusinessDetails.BusinessType,
                seller.BusinessDetails.GstNumber,
                seller.BusinessDetails.PanNumber,
                seller.BusinessDetails.CinNumber,
                seller.BusinessDetails.BusinessAddress,
                seller.BusinessDetails.Pincode);
        var bank = seller.BankDetails is null
            ? null
            : new SellerBankInfoRequest(
                seller.BankDetails.AccountHolderName ?? string.Empty,
                seller.BankDetails.BankName ?? string.Empty,
                seller.BankDetails.AccountNumberMasked ?? string.Empty,
                seller.BankDetails.IfscCode ?? string.Empty);
        var warehouses = seller.Warehouses.Select(x => new WarehouseRequest(x.Id, x.Name, x.Address, x.ContactPerson, x.ContactNumber, x.Pincode)).ToArray();
        var documents = seller.Documents.Select(x => new SellerDocumentRequest(x.DocumentType, x.FileName, x.FileUrl, x.ContentType)).ToArray();
        return new SellerOnboardingStatusDto(seller.Status.ToString(), business, bank, warehouses, documents, seller.ReviewNotes);
    }

    private static List<string> ValidateOnboarding(SellerOnboardingRequest request)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(request.Business.BusinessName) || request.Business.BusinessName.Length < 3) errors.Add("Business name must be at least 3 characters.");
        if (string.IsNullOrWhiteSpace(request.Business.LegalBusinessName) || request.Business.LegalBusinessName.Length < 3) errors.Add("Legal business name is required.");
        if (string.IsNullOrWhiteSpace(request.Business.BusinessAddress) || request.Business.BusinessAddress.Length < 10) errors.Add("Business address must be at least 10 characters.");
        if (!Regex.IsMatch(request.Business.Pincode ?? string.Empty, "^[1-9][0-9]{5}$")) errors.Add("Business pincode must be a valid 6 digit Indian pincode.");
        if (!string.IsNullOrWhiteSpace(request.Business.GstNumber) && !Regex.IsMatch(request.Business.GstNumber, "^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][1-9A-Z]Z[0-9A-Z]$")) errors.Add("GST number format is invalid.");
        if (!string.IsNullOrWhiteSpace(request.Business.PanNumber) && !Regex.IsMatch(request.Business.PanNumber, "^[A-Z]{5}[0-9]{4}[A-Z]$")) errors.Add("PAN number format is invalid.");
        if (!string.IsNullOrWhiteSpace(request.Business.CinNumber) && request.Business.CinNumber.Length < 10) errors.Add("CIN number format is invalid.");
        if (string.IsNullOrWhiteSpace(request.Bank.AccountHolderName) || !Regex.IsMatch(request.Bank.AccountHolderName, "^[A-Za-z ]{3,}$")) errors.Add("Account holder name is invalid.");
        if (string.IsNullOrWhiteSpace(request.Bank.BankName)) errors.Add("Bank name is required.");
        if (!Regex.IsMatch(request.Bank.AccountNumber ?? string.Empty, "^[0-9]{9,18}$")) errors.Add("Account number must be 9 to 18 digits.");
        if (!Regex.IsMatch(request.Bank.IfscCode ?? string.Empty, "^[A-Z]{4}0[A-Z0-9]{6}$")) errors.Add("IFSC code format is invalid.");
        if (request.Warehouses.Count == 0) errors.Add("At least one warehouse is required.");
        foreach (var warehouse in request.Warehouses)
        {
            if (string.IsNullOrWhiteSpace(warehouse.Name) || string.IsNullOrWhiteSpace(warehouse.Address) || string.IsNullOrWhiteSpace(warehouse.ContactPerson)) errors.Add("Every warehouse needs name, address and contact person.");
            if (!Regex.IsMatch(warehouse.ContactNumber ?? string.Empty, "^[6-9][0-9]{9}$")) errors.Add("Warehouse contact number must be a valid 10 digit mobile number.");
            if (!Regex.IsMatch(warehouse.Pincode ?? string.Empty, "^[1-9][0-9]{5}$")) errors.Add("Warehouse pincode must be valid.");
        }

        return errors.Distinct().ToList();
    }

    private static void ReplaceWarehouses(Seller seller, IReadOnlyList<WarehouseRequest> warehouses)
    {
        seller.Warehouses.Clear();
        foreach (var request in warehouses)
        {
            seller.Warehouses.Add(new Warehouse
            {
                SellerId = seller.Id,
                Name = request.Name.Trim(),
                Address = request.Address.Trim(),
                ContactPerson = request.ContactPerson.Trim(),
                ContactNumber = request.ContactNumber.Trim(),
                Pincode = request.Pincode.Trim()
            });
        }
    }

    private static void UpsertDocuments(Seller seller, IReadOnlyList<SellerDocumentRequest> documents)
    {
        seller.Documents.Clear();
        foreach (var request in documents.Where(x => !string.IsNullOrWhiteSpace(x.FileUrl)))
        {
            seller.Documents.Add(new SellerDocument
            {
                SellerId = seller.Id,
                DocumentType = request.DocumentType,
                FileName = request.FileName.Trim(),
                FileUrl = request.FileUrl,
                ContentType = request.ContentType,
                Status = SellerDocumentStatus.Uploaded
            });
        }
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static string MaskAccount(string value) => new string('*', Math.Max(0, value.Length - 4)) + Last4(value);
    private static string Last4(string value) => value.Length <= 4 ? value : value[^4..];
}
