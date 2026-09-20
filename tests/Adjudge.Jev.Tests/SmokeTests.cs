using System.Reflection;

namespace Adjudge.Jev.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_WhenLoadedByName_ResolvesTargetAssembly()
    {
        var assembly = Assembly.Load("Adjudge.Jev");

        assembly.GetName().Name.ShouldBe("Adjudge.Jev");
    }
}
