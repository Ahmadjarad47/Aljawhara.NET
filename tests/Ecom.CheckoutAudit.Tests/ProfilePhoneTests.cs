using System.ComponentModel.DataAnnotations;
using Ecom.Application.DTOs.Auth;
using Ecom.Infrastructure.WhatsApp;

namespace Ecom.CheckoutAudit.Tests;

public sealed class ProfilePhoneTests
{
    [Theory]
    [InlineData("50000000", "+96550000000")]
    [InlineData("60000000", "+96560000000")]
    [InlineData("90000000", "+96590000000")]
    [InlineData("96550000000", "+96550000000")]
    [InlineData("+965 5000-0000", "+96550000000")]
    [InlineData("0096550000000", "+96550000000")]
    [InlineData("+4915207610021", "+4915207610021")]
    [InlineData("004915207610021", "+4915207610021")]
    public void Profile_accepts_local_Kuwaiti_and_explicit_international_numbers(string input, string expected)
    {
        var dto = new UpdatePhoneNumberDto { PhoneNumber = input };
        var errors = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(dto, new ValidationContext(dto), errors, true),
            string.Join("; ", errors.Select(e => e.ErrorMessage)));
        Assert.Equal(expected, WhatsAppPhone.Normalize(dto.PhoneNumber, new GupshupOptions()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("5000000")]
    [InlineData("50000000x")]
    [InlineData("+965500000000000000000")]
    public void Profile_rejects_missing_short_malformed_and_overlong_input(string input)
    {
        var dto = new UpdatePhoneNumberDto { PhoneNumber = input };
        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), [], true));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("4915207610021")]
    public void Missing_prefix_does_not_bypass_Kuwaiti_number_validation(string input) =>
        Assert.Null(WhatsAppPhone.Normalize(input, new GupshupOptions()));
}
