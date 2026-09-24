using CasMcpConnectorServices.EngagementManager;
using Xunit;

namespace CasMcpConnectorServices.Tests;

public class EngagementManagerEndpointsTests
{
    [Fact]
    public void EngagementItems_BuildsV4ItemsPath()
    {
        string path = EngagementManagerEndpoints.EngagementItems("225816");

        Assert.Equal("/Api/EngagementManagement/Binder/v4/225816/Items", path);
    }
}
