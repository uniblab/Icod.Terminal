namespace Icod.Terminal.Tests;

using System.Reflection;
using Xunit;

public sealed class AssemblySmokeTests {
	[Fact]
	public void LibraryAssemblyContainsFoundationMarker() {
		Assembly assembly = Assembly.Load("Icod.Terminal");
		Type? marker = assembly.GetType(
			"Icod.Terminal.AssemblyMarker",
			throwOnError: false);

		Assert.NotNull(marker);
	}
}
