namespace FlipShop.Domain.Enums;

public enum UserRoleName { Customer, Seller, Admin, SuperAdmin, WarehouseManager }
public enum SellerStatus { Draft, PendingOtp, PendingKyc, PendingVerification, UnderReview, PendingApproval, Approved, Rejected, Suspended }
public enum BusinessType { Individual, Proprietorship, Partnership, LLP, PrivateLimited }
public enum SellerDocumentType { PanCard, AadhaarCard, GstCertificate, BusinessRegistrationCertificate }
public enum SellerDocumentStatus { Uploaded, RequestedAgain, Verified, Rejected }
public enum ProductApprovalStatus { Draft, PendingApproval, Approved, Rejected, Inactive }
public enum OrderStatus { Placed, Confirmed, Packed, ReadyToShip, Shipped, OutForDelivery, Delivered, Returned, Refunded, Cancelled }
public enum PaymentMethod { COD, Online }
public enum PaymentStatus { Pending, Paid, Failed, Refunded }
public enum OtpPurpose { CustomerLogin, SellerLogin, Registration, OrderConfirmation }
public enum EmailStatus { Queued, Sent, Failed }
