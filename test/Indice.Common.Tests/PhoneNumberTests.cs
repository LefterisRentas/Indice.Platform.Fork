﻿using Indice.Globalization;
using Xunit;

namespace Indice.Common.Tests;

public class PhoneNumberTests
{
    public PhoneNumberTests() {

    }

    [Trait("Tag", "PhoneNumber")]
    [Theory]
    [InlineData("+1 (0115) 123 4567")]
    [InlineData("+30 6972233445")]
    [InlineData("+30 (697) 2233445")]
    [InlineData("+30 210-6985955")]
    [InlineData("+44 (0141) 345 9822")]
    [InlineData("+1 800-444-4444")]
    public void ParsePhoneNumbersWithCallingCode_Success(string number) {
        var result = PhoneNumber.TryParse(number, out var phoneNumber);
        Assert.True(result);
    }

    [Theory]
    [InlineData("6972233445")]
    [InlineData("+30 6972233445")]
    [InlineData("+30 (697) 2233445")]
    public void ParseGreekPhoneNumbers(string number) {
        var result = PhoneNumber.TryParse(number, out var phoneNumber);
        Assert.True(result);
        Assert.Equal("GR", phoneNumber.TwoLetterCountryCode);
    }
}
