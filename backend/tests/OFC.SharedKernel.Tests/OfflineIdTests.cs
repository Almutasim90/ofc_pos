using OFC.SharedKernel;
using Xunit;

namespace OFC.SharedKernel.Tests;

public sealed class OfflineIdTests
{
    [Fact]
    public void New_creates_a_version_7_guid()
    {
        var id = OfflineId.New();

        Assert.Equal(7, id.Version);
    }
}
