using FluentAssertions;
using FluentValidation;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.DTOs.Reviews;
using Vitorize.Application.DTOs.Tickets;
using Vitorize.Application.DTOs.Wallet;
using Vitorize.Application.Validators.Auth;
using Vitorize.Application.Validators.Checkout;
using Vitorize.Application.Validators.Coupons;
using Vitorize.Application.Validators.Reviews;
using Vitorize.Application.Validators.Tickets;
using Vitorize.Application.Validators.Wallet;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class FluentValidatorUnitTests
{
    [Fact]
    public void Login_validator_accepts_valid_credentials_and_rejects_bad_mobile_or_empty_password()
    {
        AssertValid(new LoginRequestValidator(), new LoginRequestDto { Mobile = "09123456789", Password = "password" });
        AssertInvalid(new LoginRequestValidator(), new LoginRequestDto { Mobile = "9123456789", Password = "" }, "Mobile", "Password");
    }

    [Fact]
    public void Registration_validator_enforces_name_mobile_email_and_password_bounds()
    {
        AssertValid(new RegisterRequestValidator(), new RegisterRequestDto
        {
            FullName = "Test User", Mobile = "09123456789", Email = "test@example.com", Password = "12345678"
        });
        AssertInvalid(new RegisterRequestValidator(), new RegisterRequestDto
        {
            FullName = "", Mobile = "invalid", Email = "invalid", Password = "short"
        }, "FullName", "Mobile", "Email", "Password");
    }

    [Fact]
    public void Change_password_validator_requires_current_strong_matching_new_password()
    {
        AssertValid(new ChangePasswordRequestValidator(), new ChangePasswordRequestDto
        {
            CurrentPassword = "old-password", NewPassword = "new-password", ConfirmNewPassword = "new-password"
        });
        AssertInvalid(new ChangePasswordRequestValidator(), new ChangePasswordRequestDto
        {
            CurrentPassword = "", NewPassword = "short", ConfirmNewPassword = "different"
        }, "CurrentPassword", "NewPassword", "ConfirmNewPassword");
    }

    [Fact]
    public void Reset_password_validator_enforces_mobile_six_digit_code_and_confirmation()
    {
        AssertValid(new ResetPasswordRequestValidator(), new ResetPasswordRequestDto
        {
            Mobile = "09123456789", Code = "123456", NewPassword = "new-password", ConfirmNewPassword = "new-password"
        });
        AssertInvalid(new ResetPasswordRequestValidator(), new ResetPasswordRequestDto
        {
            Mobile = "invalid", Code = "12ab", NewPassword = "short", ConfirmNewPassword = "different"
        }, "Mobile", "Code", "NewPassword", "ConfirmNewPassword");
    }

    [Fact]
    public void Otp_request_validator_rejects_undefined_and_two_factor_purpose()
    {
        AssertValid(new SendOtpRequestValidator(), new SendOtpRequestDto
        {
            Mobile = "09123456789", Purpose = (byte)OtpPurpose.MobileVerification
        });
        AssertInvalid(new SendOtpRequestValidator(), new SendOtpRequestDto
        {
            Mobile = "invalid", Purpose = (byte)OtpPurpose.TwoFactorAuthentication
        }, "Mobile", "Purpose");
        AssertInvalid(new SendOtpRequestValidator(), new SendOtpRequestDto
        {
            Mobile = "09123456789", Purpose = byte.MaxValue
        }, "Purpose");
    }

    [Fact]
    public void Otp_verification_validator_enforces_contract_and_defined_purpose()
    {
        AssertValid(new VerifyOtpRequestValidator(), new VerifyOtpRequestDto
        {
            Mobile = "09123456789", Code = "123456", Purpose = (byte)OtpPurpose.MobileVerification
        });
        AssertInvalid(new VerifyOtpRequestValidator(), new VerifyOtpRequestDto
        {
            Mobile = "invalid", Code = "12345x", Purpose = byte.MaxValue
        }, "Mobile", "Code", "Purpose");
    }

    [Theory]
    [InlineData("+989123456789", "1234")]
    [InlineData("۰۹۱۲۳۴۵۶۷۸۹", "12345678")]
    public void Otp_login_validator_accepts_normalized_iran_mobile_and_four_to_eight_digit_code(string mobile, string code)
    {
        AssertValid(new RequestOtpLoginRequestValidator(), new RequestOtpLoginRequestDto { Mobile = mobile });
        AssertValid(new VerifyOtpLoginRequestValidator(), new VerifyOtpLoginRequestDto { Mobile = mobile, Code = code });
    }

    [Fact]
    public void Otp_login_validator_rejects_invalid_mobile_or_code()
    {
        AssertInvalid(new RequestOtpLoginRequestValidator(), new RequestOtpLoginRequestDto { Mobile = "02112345678" }, "Mobile");
        AssertInvalid(new VerifyOtpLoginRequestValidator(), new VerifyOtpLoginRequestDto
        {
            Mobile = "02112345678", Code = "12ab"
        }, "Mobile", "Code");
    }

    [Fact]
    public void Profile_validator_allows_missing_email_but_rejects_invalid_or_oversized_values()
    {
        AssertValid(new UpdateProfileRequestValidator(), new UpdateProfileRequestDto { FullName = "Test User" });
        AssertInvalid(new UpdateProfileRequestValidator(), new UpdateProfileRequestDto
        {
            FullName = "", Email = "invalid"
        }, "FullName", "Email");
    }

    [Fact]
    public void Forgot_password_validator_requires_canonical_mobile()
    {
        AssertValid(new ForgotPasswordRequestValidator(), new ForgotPasswordRequestDto { Mobile = "09123456789" });
        AssertInvalid(new ForgotPasswordRequestValidator(), new ForgotPasswordRequestDto { Mobile = "+989123456789" }, "Mobile");
    }

    [Fact]
    public void Refresh_and_logout_validators_require_refresh_token()
    {
        AssertValid(new RefreshTokenRequestValidator(), new RefreshTokenRequestDto { RefreshToken = "token" });
        AssertInvalid(new RefreshTokenRequestValidator(), new RefreshTokenRequestDto(), "RefreshToken");
        AssertValid(new LogoutRequestValidator(), new LogoutRequestDto { RefreshToken = "token" });
        AssertInvalid(new LogoutRequestValidator(), new LogoutRequestDto(), "RefreshToken");
    }

    [Fact]
    public void Checkout_validator_bounds_optional_description_and_coupon()
    {
        AssertValid(new CheckoutRequestValidator(), new CheckoutRequestDto());
        AssertValid(new CheckoutRequestValidator(), new CheckoutRequestDto { Description = "note", CouponCode = "SUMMER" });
        AssertInvalid(new CheckoutRequestValidator(), new CheckoutRequestDto
        {
            Description = new string('x', 1001), CouponCode = new string('x', 101)
        }, "Description", "CouponCode");
    }

    [Fact]
    public void Coupon_validator_requires_code_and_positive_order_amount()
    {
        AssertValid(new ValidateCouponRequestValidator(), new ValidateCouponRequestDto { Code = "SUMMER", OrderAmount = 1 });
        AssertInvalid(new ValidateCouponRequestValidator(), new ValidateCouponRequestDto { Code = "", OrderAmount = 0 }, "Code", "OrderAmount");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(500_000_000)]
    public void Wallet_top_up_accepts_boundary_amounts(decimal amount)
    {
        AssertValid(new WalletTopUpRequestValidator(), new WalletTopUpRequestDto { Amount = amount });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(500_000_001)]
    public void Wallet_top_up_rejects_nonpositive_or_excessive_amount(decimal amount)
    {
        AssertInvalid(new WalletTopUpRequestValidator(), new WalletTopUpRequestDto { Amount = amount }, "Amount");
    }

    [Fact]
    public void Wallet_credit_and_debit_require_user_positive_amount_and_bounded_description()
    {
        var userId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        AssertValid(new WalletChargeRequestValidator(), new WalletChargeRequestDto { UserId = userId, Amount = 1 });
        AssertInvalid(new WalletChargeRequestValidator(), new WalletChargeRequestDto
        {
            UserId = Guid.Empty, Amount = 0, Description = new string('x', 1001)
        }, "UserId", "Amount", "Description");
        AssertValid(new WalletWithdrawRequestValidator(), new WalletWithdrawRequestDto { UserId = userId, Amount = 1 });
        AssertInvalid(new WalletWithdrawRequestValidator(), new WalletWithdrawRequestDto
        {
            UserId = Guid.Empty, Amount = -1, Description = new string('x', 1001)
        }, "UserId", "Amount", "Description");
    }

    [Fact]
    public void Ticket_create_validator_enforces_content_classification_and_attachment_limits()
    {
        AssertValid(new CreateTicketRequestValidator(), new CreateTicketRequestDto
        {
            Subject = "Support", Message = "Please help", Department = 1, Priority = 1
        });
        AssertInvalid(new CreateTicketRequestValidator(), new CreateTicketRequestDto
        {
            Subject = "", Message = "", Department = 0, Priority = 0, AttachmentPath = new string('x', 501)
        }, "Subject", "Message", "Department", "Priority", "AttachmentPath");
    }

    [Fact]
    public void Customer_and_admin_ticket_messages_share_safe_length_contract()
    {
        AssertValid(new AddTicketMessageRequestValidator(), new AddTicketMessageRequestDto { Message = "reply" });
        AssertInvalid(new AddTicketMessageRequestValidator(), new AddTicketMessageRequestDto
        {
            Message = "", AttachmentPath = new string('x', 501)
        }, "Message", "AttachmentPath");
        AssertValid(new AdminAddTicketMessageRequestValidator(), new AdminAddTicketMessageRequestDto { Message = "reply" });
        AssertInvalid(new AdminAddTicketMessageRequestValidator(), new AdminAddTicketMessageRequestDto
        {
            Message = new string('x', 5001), AttachmentPath = new string('x', 501)
        }, "Message", "AttachmentPath");
    }

    [Fact]
    public void Review_create_and_update_enforce_rating_content_and_optional_title_bounds()
    {
        var productId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        AssertValid(new CreateProductReviewRequestValidator(), new CreateProductReviewRequestDto
        {
            ProductId = productId, Comment = "Useful", Rating = 5
        });
        AssertInvalid(new CreateProductReviewRequestValidator(), new CreateProductReviewRequestDto
        {
            ProductId = Guid.Empty, Comment = "", Title = new string('x', 201), Rating = 0
        }, "ProductId", "Comment", "Title", "Rating");
        AssertValid(new UpdateProductReviewRequestValidator(), new UpdateProductReviewRequestDto { Comment = "Updated", Rating = 1 });
        AssertInvalid(new UpdateProductReviewRequestValidator(), new UpdateProductReviewRequestDto
        {
            Comment = new string('x', 2001), Title = new string('x', 201), Rating = 6
        }, "Comment", "Title", "Rating");
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(0, false)]
    [InlineData(3, false)]
    public void Review_vote_allows_only_helpful_or_unhelpful(byte vote, bool expected)
    {
        var result = new ProductReviewVoteRequestValidator().Validate(new ProductReviewVoteRequestDto { VoteType = vote });
        result.IsValid.Should().Be(expected);
    }

    private static void AssertValid<T>(IValidator<T> validator, T model)
    {
        validator.Validate(model).IsValid.Should().BeTrue();
    }

    private static void AssertInvalid<T>(IValidator<T> validator, T model, params string[] properties)
    {
        var result = validator.Validate(model);
        result.IsValid.Should().BeFalse();
        foreach (var property in properties)
            result.Errors.Should().Contain(x => x.PropertyName == property, $"{property} should be rejected");
    }
}
