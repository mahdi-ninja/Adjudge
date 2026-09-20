using System.Reflection;

namespace Adjudge.Testing.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void Assembly_WhenLoadedByName_ResolvesTargetAssembly()
    {
        var assembly = Assembly.Load("Adjudge.Testing");

        assembly.GetName().Name.ShouldBe("Adjudge.Testing");
    }
}
