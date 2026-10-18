using Ecom.Domain.Entity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Xunit;

public class AuthenticationTests
{
    [Fact] public void Existing_identity_v2_and_v3_hashes_remain_verifiable() {
        var user=new AppUsers(); const string password="synthetic-test-only-123!";
        var current=new PasswordHasher<AppUsers>();
        foreach(var mode in new[]{PasswordHasherCompatibilityMode.IdentityV2,PasswordHasherCompatibilityMode.IdentityV3}) {
            var legacy=new PasswordHasher<AppUsers>(Options.Create(new PasswordHasherOptions{CompatibilityMode=mode}));
            var hash=legacy.HashPassword(user,password);
            Assert.NotEqual(PasswordVerificationResult.Failed,current.VerifyHashedPassword(user,hash,password));
            Assert.Equal(PasswordVerificationResult.Failed,current.VerifyHashedPassword(user,hash,"incorrect"));
        }
    }
}
