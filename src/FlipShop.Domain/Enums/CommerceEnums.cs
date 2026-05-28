namespace FlipShop.Domain.Enums;

public enum UserRoleName { Customer, Seller, Admin }
public enum SellerStatus { PendingOtp, PendingKyc, PendingApproval, Approved, Suspended }
public enum ProductApprovalStatus { Draft, PendingApproval, Approved, Rejected }
public enum OrderStatus { Placed, Confirmed, Packed, Shipped, Delivered, Cancelled }
public enum PaymentMethod { COD, Online }
public enum PaymentStatus { Pending, Paid, Failed, Refunded }
public enum OtpPurpose { CustomerLogin, SellerLogin, Registration, OrderConfirmation }
public enum EmailStatus { Queued, Sent, Failed }
