namespace Adjudge.Jev.Tests;

// Every test class in this assembly joins this collection: the options resolve the TYPESAFE_* variables
// per request, so a class mutating them in parallel with one sending a request changes what it sends.
[CollectionDefinition(Name)]
public sealed class SharedEnvironment
{
    public const string Name = "environment";
}
