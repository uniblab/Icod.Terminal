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
namespace Icod.Terminal.Tests.Query;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Freezes correlation boundaries for Terminal 1.27 environment queries.
/// </summary>
public sealed class TerminalEnvironmentCorrelationTests {
	[Theory]
	[InlineData( "\u001b[?997;1n", true )]
	[InlineData( "\u001b[?997;9n", true )]
	[InlineData( "\u001b[?997;1:2n", true )]
	[InlineData( "\u001b[?998;1n", false )]
	[InlineData( "\u001b[48;24;80;0;0t", false )]
	public void AppearanceMatcherClaimsOnlyAppearanceGrammar(
		string bytes,
		bool expected
	) {
		Assert.Equal(
			expected,
			TerminalEnvironmentProtocol.AppearanceReportMatcher.IsMatch(
				CreateFrame( bytes )
			)
		);
	}

	[Theory]
	[InlineData( "\u001b[?2031;1$y", 2031, true )]
	[InlineData( "\u001b[?2031;9$y", 2031, true )]
	[InlineData( "\u001b[?2048;2$y", 2031, false )]
	[InlineData( "\u001b[?2031;1n", 2031, false )]
	[InlineData( "\u001b[?2048;4$y", 2048, true )]
	public void ModeMatcherClaimsOnlyItsModeGrammar(
		string bytes,
		int mode,
		bool expected
	) {
		ITerminalResponseMatcher matcher =
			TerminalEnvironmentProtocol.CreatePrivateModeReportMatcher( mode );

		Assert.Equal( expected, matcher.IsMatch( CreateFrame( bytes ) ) );
	}

	[Theory]
	[InlineData( "\u001b[?997;", true )]
	[InlineData( "\u001b[?997", false )]
	[InlineData( "\u001b[?998;", false )]
	[InlineData( "?997;", true )]
	public void AppearanceCorrelationRequiresCompleteSelectorField(
		string bytes,
		bool expected
	) {
		ICorrelatedTerminalResponseMatcher matcher =
			Assert.IsAssignableFrom<ICorrelatedTerminalResponseMatcher>(
				TerminalEnvironmentProtocol.AppearanceReportMatcher
			);
		IReadOnlyList<byte> encoded = bytes.StartsWith( "?", StringComparison.Ordinal )
			? [ 0x9B, .. Encoding.ASCII.GetBytes( bytes ) ]
			: Encoding.ASCII.GetBytes( bytes );

		Assert.Equal( expected, matcher.IsCorrelatedPrefix( encoded ) );
	}

	[Theory]
	[InlineData( "\u001b[?2031;", 2031, true )]
	[InlineData( "\u001b[?2031", 2031, false )]
	[InlineData( "\u001b[?2048;", 2031, false )]
	[InlineData( "?2048;", 2048, true )]
	public void ModeCorrelationRequiresCompleteModeField(
		string bytes,
		int mode,
		bool expected
	) {
		ICorrelatedTerminalResponseMatcher matcher =
			Assert.IsAssignableFrom<ICorrelatedTerminalResponseMatcher>(
				TerminalEnvironmentProtocol.CreatePrivateModeReportMatcher( mode )
			);
		IReadOnlyList<byte> encoded = bytes.StartsWith( "?", StringComparison.Ordinal )
			? [ 0x9B, .. Encoding.ASCII.GetBytes( bytes ) ]
			: Encoding.ASCII.GetBytes( bytes );

		Assert.Equal( expected, matcher.IsCorrelatedPrefix( encoded ) );
	}

	private static TerminalResponseFrame CreateFrame(
		string bytes
	) {
		return new TerminalResponseFrame(
			TerminalResponseFrameKind.Csi,
			Encoding.Latin1.GetBytes( bytes )
		);
	}
}
