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
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies the C163 bounded CSI terminal/cell pixel-geometry protocol.
/// </summary>
public sealed class TerminalCsiGeometryTests {
	[Fact]
	public void GeometryRequestsUseCanonicalSevenBitCsi() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[14t" ),
			TerminalCsiGeometryProtocol.TerminalPixelSizeRequest.ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[16t" ),
			TerminalCsiGeometryProtocol.CellPixelSizeRequest.ToArray()
		);
	}

	[Fact]
	public void TerminalPixelResponseUsesHeightThenWidthWireOrder() {
		TerminalPixelSize size = TerminalCsiGeometryProtocol.ParseTerminalPixelSize(
			CreateFrame( "\u001b[4;800;1200t" )
		);

		Assert.Equal( 1200, size.Width );
		Assert.Equal( 800, size.Height );
	}

	[Fact]
	public void CellPixelResponseAcceptsEightBitCsi() {
		TerminalResponseFrame frame = new(
			TerminalResponseFrameKind.Csi,
			new byte[] {
				0x9B,
				(byte)'6',
				(byte)';',
				(byte)'2',
				(byte)'0',
				(byte)';',
				(byte)'1',
				(byte)'0',
				(byte)'t'
			}
		);

		TerminalPixelSize size = TerminalCsiGeometryProtocol.ParseCellPixelSize( frame );

		Assert.Equal( 10, size.Width );
		Assert.Equal( 20, size.Height );
	}

	[Fact]
	public void GeometryMatchersDistinguishTerminalAndCellResponses() {
		TerminalResponseFrame terminal = CreateFrame( "\u001b[4;800;1200t" );
		TerminalResponseFrame cell = CreateFrame( "\u001b[6;20;10t" );

		Assert.True( TerminalCsiGeometryProtocol.TerminalPixelSizeMatcher.IsMatch( terminal ) );
		Assert.False( TerminalCsiGeometryProtocol.TerminalPixelSizeMatcher.IsMatch( cell ) );
		Assert.False( TerminalCsiGeometryProtocol.CellPixelSizeMatcher.IsMatch( terminal ) );
		Assert.True( TerminalCsiGeometryProtocol.CellPixelSizeMatcher.IsMatch( cell ) );
	}

	[Theory]
	[InlineData( "\u001b[4;0;1200t" )]
	[InlineData( "\u001b[4;800;0t" )]
	[InlineData( "\u001b[6;20t" )]
	[InlineData( "\u001b[6;20:1;10t" )]
	[InlineData( "\u001b[6;1000001;10t" )]
	public void MalformedGeometryResponsesFailDeterministically(
		string wire
	) {
		TerminalResponseFrame frame = CreateFrame( wire );

		Assert.Throws<FormatException>(
			() => {
				if ( wire.Contains( "[4;", StringComparison.Ordinal ) ) {
					_ = TerminalCsiGeometryProtocol.ParseTerminalPixelSize( frame );
				} else {
					_ = TerminalCsiGeometryProtocol.ParseCellPixelSize( frame );
				}
			}
		);
	}

	[Fact]
	public void ExactCharacterGridDerivesCellPixels() {
		bool success = TerminalPixelGeometry.TryDeriveCellPixelSize(
			new TerminalSize( 120, 40 ),
			new TerminalPixelSize( 1200, 800 ),
			out TerminalPixelSize cellSize
		);

		Assert.True( success );
		Assert.Equal( 10, cellSize.Width );
		Assert.Equal( 20, cellSize.Height );
	}

	[Fact]
	public void FractionalCharacterGridDoesNotFabricateCellPixels() {
		bool success = TerminalPixelGeometry.TryDeriveCellPixelSize(
			new TerminalSize( 119, 40 ),
			new TerminalPixelSize( 1200, 800 ),
			out _
		);

		Assert.False( success );
	}

	private static TerminalResponseFrame CreateFrame(
		string wire
	) {
		ArgumentNullException.ThrowIfNull( wire );
		return new TerminalResponseFrame(
			TerminalResponseFrameKind.Csi,
			Encoding.ASCII.GetBytes( wire )
		);
	}
}
