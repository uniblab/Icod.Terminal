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
namespace Icod.Terminal.Tests.Control;

using System.Reflection;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies reversible UTF-8 ownership for byte-oriented Windows console output.
/// </summary>
public sealed class WindowsConsoleOutputCodePageTests {
	private const uint Utf8CodePage = 65001U;

	[Fact]
	public void AcquiresUtf8AndRestoresTheExactBaselineOnce() {
		uint current = 437U;
		List<uint> writes = [];
		IDisposable? lease = InvokeTryEnableUtf8(
			() => current,
			value => {
				writes.Add( value );
				current = value;
				return true;
			}
		);

		Assert.NotNull( lease );
		Assert.Equal( Utf8CodePage, current );
		Assert.Equal( [ Utf8CodePage ], writes );

		lease.Dispose();

		Assert.Equal( 437U, current );
		Assert.Equal( [ Utf8CodePage, 437U ], writes );

		lease.Dispose();

		Assert.Equal( [ Utf8CodePage, 437U ], writes );
	}

	[Fact]
	public void LeavesAnExistingUtf8BaselineUntouched() {
		List<uint> writes = [];
		IDisposable? lease = InvokeTryEnableUtf8(
			() => Utf8CodePage,
			value => {
				writes.Add( value );
				return true;
			}
		);

		Assert.NotNull( lease );
		lease.Dispose();
		Assert.Empty( writes );
	}

	[Fact]
	public void ReportsUnavailableWhenUtf8CannotBeEnabled() {
		IDisposable? lease = InvokeTryEnableUtf8(
			() => 437U,
			_ => false
		);

		Assert.Null( lease );
	}

	private static IDisposable? InvokeTryEnableUtf8(
		Func<uint> getOutputCodePage,
		Func<uint, bool> setOutputCodePage
	) {
		Type? owner = typeof( TerminalSession ).Assembly.GetType(
			"Icod.Terminal.WindowsConsoleOutputCodePage",
			throwOnError: false
		);
		Assert.NotNull( owner );
		MethodInfo? method = owner.GetMethod(
			"TryEnableUtf8",
			BindingFlags.Static | BindingFlags.NonPublic,
			binder: null,
			types: [ typeof( Func<uint> ), typeof( Func<uint, bool> ) ],
			modifiers: null
		);
		Assert.NotNull( method );
		return (IDisposable?)method.Invoke(
			null,
			[ getOutputCodePage, setOutputCodePage ]
		);
	}
}
