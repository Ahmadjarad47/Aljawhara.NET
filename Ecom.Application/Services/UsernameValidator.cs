using System.Globalization;
using System.Text;
using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Identity;

namespace Ecom.Application.Services;

public sealed class UsernameValidator : IUserValidator<AppUsers>
{
    private static bool IsValid(string username)
    {
        var afterSpace = true;
        foreach (var rune in username.EnumerateRunes())
        {
            if (rune.Value == ' ')
            {
                if (afterSpace) return false;
                afterSpace = true;
                continue;
            }

            if (!Rune.IsLetter(rune) && Rune.GetUnicodeCategory(rune) is not (
                    UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or
                    UnicodeCategory.EnclosingMark or UnicodeCategory.DecimalDigitNumber) &&
                rune.Value is not ('.' or '_' or '@' or '+' or '-'))
                return false;

            afterSpace = false;
        }

        return !afterSpace;
    }

    public async Task<IdentityResult> ValidateAsync(UserManager<AppUsers> manager, AppUsers user)
    {
        var username = await manager.GetUserNameAsync(user);
        return username is not null && IsValid(username)
            ? IdentityResult.Success
            : IdentityResult.Failed(new IdentityError
            {
                Code = nameof(IdentityErrorDescriber.InvalidUserName),
                Description = "Username may contain letters, digits, single spaces, and these symbols: - . _ @ +."
            });
    }
}
