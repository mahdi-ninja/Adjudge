using System.Runtime.CompilerServices;
using DiffEngine;

namespace Adjudge.Jev.Tests;

internal static class VerifyInitializer
{
    [ModuleInitializer]
    public static void Initialize() => DiffRunner.Disabled = true;
}
