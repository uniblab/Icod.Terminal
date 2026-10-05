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
namespace Icod.Terminal.Tests.Input;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Freezes the Terminal 1.27 environment wire grammar and numeric policy.
/// </summary>
public sealed class TerminalEnvironmentProtocolTests {
	[Fact]
	public void RequestsAndModeWritesUseCanonicalSevenBitCsi() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[?996n" ),
			TerminalEnvironmentProtocol.AppearanceQueryRequest.ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[?2031$p" ),
			TerminalEnvironmentProtocol.CreatePrivateModeQuery( 2031 ).ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[?2048$p" ),
			TerminalEnvironmentProtocol.CreatePrivateModeQuery( 2048 ).ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[?2031h" ),
			TerminalEnvironmentProtocol.CreatePrivateModeSet( 2031, enabled: true ).ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[?2031l" ),
			TerminalEnvironmentProtocol.CreatePrivateModeSet( 2031, enabled: false ).ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[?2048h" ),
			TerminalEnvironmentProtocol.CreatePrivateModeSet( 2048, enabled: true ).ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[?2048l" ),
			TerminalEnvironmentProtocol.CreatePrivateModeSet( 2048, enabled: false ).ToArray()
		);
	}

	[Theory]
	[InlineData( "\u001b[?997;1n", false, TerminalAppearance.Dark )]
	[InlineData( "\u001b[?997;2n", false, TerminalAppearance.Light )]
	[InlineData( "?997;1n", true, TerminalAppearance.Dark )]
	[InlineData( "?997;2n", true, TerminalAppearance.Light )]
	public void ParsesSevenAndEightBitAppearanceReports(
		string body,
		bool eightBit,
		TerminalAppearance expected
	) {
		TerminalResponseFrame frame = CreateCsiFrame( body, eightBit );

		Assert.Equal(
			expected,
			TerminalEnvironmentProtocol.ParseAppearance( frame )
		);
	}

	[Theory]
	[InlineData( 0, 0 )]
	[InlineData( 1, 1 )]
	[InlineData( 2, 2 )]
	[InlineData( 3, 3 )]
	[InlineData( 4, 4 )]
	public void ParsesEveryPrivateModeState(
		int wireState,
		int expectedState
	) {
		TerminalPrivateModeState expected = (TerminalPrivateModeState)expectedState;
		TerminalResponseFrame sevenBit = CreateCsiFrame(
			$"\u001b[?2031;{wireState}$y"
		);
		TerminalResponseFrame eightBit = CreateCsiFrame(
			$"?2031;{wireState}$y",
			eightBit: true
		);

		Assert.Equal(
			expected,
			TerminalEnvironmentProtocol.ParsePrivateModeState( sevenBit, 2031 )
		);
		Assert.Equal(
			expected,
			TerminalEnvironmentProtocol.ParsePrivateModeState( eightBit, 2031 )
		);
	}

	[Theory]
	[InlineData( "\u001b[48;24;80;0;0t", 80, 24, 0, 0 )]
	[InlineData( "\u001b[48;24;80;768;1280t", 80, 24, 1280, 768 )]
	[InlineData( "48;24:1;80:2;768:9;1280:7t", 80, 24, 1280, 768 )]
	public void ParsesResizeReportsAndPreservesFieldProvenance(
		string body,
		int columns,
		int rows,
		int pixelWidth,
		int pixelHeight
	) {
		TerminalResponseFrame frame = CreateCsiFrame(
			body,
			eightBit: !body.StartsWith( "\u001b", StringComparison.Ordinal )
		);

		TerminalInBandResizeEvent resize =
			TerminalEnvironmentProtocol.ParseInBandResize( frame );

		Assert.Equal( new TerminalDimensions( columns, rows ), resize.Dimensions );
		if ( 0 == pixelWidth ) {
			Assert.Null( resize.PixelDimensions );
		} else {
			Assert.Equal(
				new TerminalPixelDimensions( pixelWidth, pixelHeight ),
				resize.PixelDimensions
			);
		}
	}

	[Theory]
	[InlineData( "\u001b[997;1n" )]
	[InlineData( "\u001b[?997n" )]
	[InlineData( "\u001b[?997;0n" )]
	[InlineData( "\u001b[?997;3n" )]
	[InlineData( "\u001b[?997;1;2n" )]
	[InlineData( "\u001b[?997;1:2n" )]
	[InlineData( "\u001b[?997;+1n" )]
	[InlineData( "\u001b[?997;2147483648n" )]
	[InlineData( "\u001b[?997;1$p" )]
	public void RejectsMalformedAppearanceReports(
		string bytes
	) {
		Assert.Throws<FormatException>(
			() => TerminalEnvironmentProtocol.ParseAppearance(
				CreateCsiFrame( bytes )
			)
		);
	}

	[Theory]
	[InlineData( "\u001b[2031;1$y", 2031 )]
	[InlineData( "\u001b[?2031$y", 2031 )]
	[InlineData( "\u001b[?2031;5$y", 2031 )]
	[InlineData( "\u001b[?2031;1;2$y", 2031 )]
	[InlineData( "\u001b[?2031;1:2$y", 2031 )]
	[InlineData( "\u001b[?2031;-1$y", 2031 )]
	[InlineData( "\u001b[?2147483648;1$y", 2031 )]
	[InlineData( "\u001b[?2048;1$y", 2031 )]
	[InlineData( "\u001b[?2031;1y", 2031 )]
	public void RejectsMalformedPrivateModeReports(
		string bytes,
		int expectedMode
	) {
		Assert.Throws<FormatException>(
			() => TerminalEnvironmentProtocol.ParsePrivateModeState(
				CreateCsiFrame( bytes ),
				expectedMode
			)
		);
	}

	[Theory]
	[InlineData( "\u001b[48;24;80;0;1t" )]
	[InlineData( "\u001b[48;24;80;1;0t" )]
	[InlineData( "\u001b[48;0;80;0;0t" )]
	[InlineData( "\u001b[48;24;0;0;0t" )]
	[InlineData( "\u001b[48;24;80;0t" )]
	[InlineData( "\u001b[48;24;80;0;0;1t" )]
	[InlineData( "\u001b[?48;24;80;0;0t" )]
	[InlineData( "\u001b[48;;80;0;0t" )]
	[InlineData( "\u001b[48;24:;80;0;0t" )]
	[InlineData( "\u001b[48;24;80;0:;0t" )]
	[InlineData( "\u001b[48;+24;80;0;0t" )]
	[InlineData( "\u001b[48;24;2147483648;0;0t" )]
	[InlineData( "\u001b[48;24;80;0;0n" )]
	public void RejectsMalformedResizeReports(
		string bytes
	) {
		Assert.Throws<FormatException>(
			() => TerminalEnvironmentProtocol.ParseInBandResize(
				CreateCsiFrame( bytes )
			)
		);
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( -1 )]
	public void RejectsInvalidPrivateModeArguments(
		int mode
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalEnvironmentProtocol.CreatePrivateModeQuery( mode )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalEnvironmentProtocol.CreatePrivateModeSet( mode, true )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalEnvironmentProtocol.CreatePrivateModeReportMatcher( mode )
		);
	}

	private static TerminalResponseFrame CreateCsiFrame(
		string bytes,
		bool eightBit = false
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		byte[] encoded = Encoding.Latin1.GetBytes( bytes );
		if ( eightBit ) {
			encoded = [ 0x9B, .. encoded ];
		}
		return new TerminalResponseFrame(
			TerminalResponseFrameKind.Csi,
			encoded
		);
	}
}
