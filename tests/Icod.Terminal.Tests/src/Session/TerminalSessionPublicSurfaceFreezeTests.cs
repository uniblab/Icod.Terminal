namespace Icod.Terminal.Tests.Session;

using System.Reflection;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Freezes the final pre-1.0 TerminalSession transport-ownership boundary.
/// </summary>
public sealed class TerminalSessionPublicSurfaceFreezeTests {
	[Fact]
	public void SessionDoesNotExposeRawInputTransport() {
		PropertyInfo? input = typeof( TerminalSession ).GetProperty(
			"Input",
			BindingFlags.Instance | BindingFlags.Public
		);

		Assert.Null( input );
		Assert.True( typeof( ITerminalInput ).IsPublic );
	}

	[Fact]
	public void SessionRetainsExplicitCustomTransportInjection() {
		MethodInfo? open = typeof( TerminalSession )
			.GetMethods( BindingFlags.Public | BindingFlags.Static )
			.SingleOrDefault(
				method => string.Equals(
					method.Name,
					nameof( TerminalSession.OpenAsync ),
					StringComparison.Ordinal
				)
				&& method.GetParameters().Any(
					parameter => parameter.ParameterType == typeof( ITerminalInput )
				)
				&& method.GetParameters().Any(
					parameter => parameter.ParameterType == typeof( ITerminalOutput )
				)
			);

		Assert.NotNull( open );
	}

	[Fact]
	public void RawOutputTransportRemainsAnExplicitAdvancedSurface() {
		PropertyInfo? output = typeof( TerminalSession ).GetProperty(
			nameof( TerminalSession.Output ),
			BindingFlags.Instance | BindingFlags.Public
		);

		Assert.NotNull( output );
		Assert.Equal( typeof( ITerminalOutput ), output.PropertyType );
	}
}
