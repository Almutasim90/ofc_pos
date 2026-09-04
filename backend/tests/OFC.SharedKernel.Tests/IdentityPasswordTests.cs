using OFC.Infrastructure.Security;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class IdentityPasswordTests
{
    [Fact]
    public void Password_hash_verifies_only_the_original_password()
    {
        var (salt, hash) = IdentityService.Hash("A-long-enough-password");

        Assert.True(IdentityService.Verify("A-long-enough-password", salt, hash));
        Assert.False(IdentityService.Verify("another-long-password", salt, hash));
    }

    [Fact]
    public void Password_hash_uses_a_unique_salt()
    {
        var first = IdentityService.Hash("A-long-enough-password");
        var second = IdentityService.Hash("A-long-enough-password");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }
}
