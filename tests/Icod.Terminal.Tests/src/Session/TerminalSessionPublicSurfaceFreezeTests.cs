/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
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
