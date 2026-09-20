using System.Reflection;

namespace Adjudge.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_WhenLoadedByName_ResolvesTargetAssembly()
    {
        var assembly = Assembly.Load("Adjudge");

        assembly.GetName().Name.ShouldBe("Adjudge");
    }
}
