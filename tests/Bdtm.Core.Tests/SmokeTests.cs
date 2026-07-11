using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void CoreProjectLoads()
    {
        Assert.NotNull(typeof(AssemblyMarker));
        Assert.Equal("Bdtm.Core", typeof(AssemblyMarker).Assembly.GetName().Name);
    }
}
