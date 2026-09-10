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
/// Defines the C104 public verification entry-point contract before implementation.
/// </summary>
public sealed class TerminalCapabilityVerificationContractTests {
	[Fact]
	public void VerificationEntryPointIsExplicitAndAsynchronous() {
		MethodInfo? method = typeof( TerminalSession ).GetMethod(
			"VerifyCapabilityAsync",
			BindingFlags.Public | BindingFlags.Instance,
			binder: null,
			[
				typeof( TerminalCapability ),
				typeof( CancellationToken )
			],
			modifiers: null
		);

		Assert.NotNull( method );
		Assert.Equal(
			typeof( ValueTask<TerminalCapabilityStatus> ),
			method!.ReturnType
		);
	}

	[Fact]
	public void VerificationDoesNotExposeProtocolOrDependencyParameters() {
		MethodInfo[] methods = typeof( TerminalSession )
			.GetMethods( BindingFlags.Public | BindingFlags.Instance )
			.Where(
				method => "VerifyCapabilityAsync" == method.Name
			)
			.ToArray();

		MethodInfo method = Assert.Single( methods );
		ParameterInfo[] parameters = method.GetParameters();
		Assert.Equal( 2, parameters.Length );
		Assert.Equal( typeof( TerminalCapability ), parameters[ 0 ].ParameterType );
		Assert.Equal( typeof( CancellationToken ), parameters[ 1 ].ParameterType );
	}
}
