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
namespace Icod.Terminal.Tests.Output;

using System.Text;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies D171 byte-exact consolidation of the released DCS query request families.
/// </summary>
public sealed class DcsQueryConsolidationTests {
	[Theory]
	[InlineData( TerminalStatusStringKind.SelectGraphicRendition, "m" )]
	[InlineData( TerminalStatusStringKind.ConformanceLevel, "\"p" )]
	[InlineData( TerminalStatusStringKind.CursorStyle, " q" )]
	[InlineData( TerminalStatusStringKind.CharacterProtection, "\"q" )]
	[InlineData( TerminalStatusStringKind.ScrollingRegion, "r" )]
	[InlineData( TerminalStatusStringKind.LeftRightMargins, "s" )]
	[InlineData( TerminalStatusStringKind.LinesPerPage, "t" )]
	[InlineData( TerminalStatusStringKind.ColumnsPerPage, "$|" )]
	[InlineData( TerminalStatusStringKind.ActiveStatusDisplay, "$}" )]
	[InlineData( TerminalStatusStringKind.StatusLineType, "$~" )]
	[InlineData( TerminalStatusStringKind.AttributeChangeExtent, "*x" )]
	[InlineData( TerminalStatusStringKind.LinesPerScreen, "*|" )]
	public void EveryDecrqssRequestRemainsByteExact(
		TerminalStatusStringKind kind,
		string identifier
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				$"\u001bP$q{identifier}\u001b\\"
			),
			TerminalDecrqssProtocol.CreateRequest( kind ).ToArray()
		);
	}

	[Theory]
	[InlineData( "ku", "6B75" )]
	[InlineData( "TN", "544E" )]
	[InlineData( "Co", "436F" )]
	[InlineData( "#2", "2332" )]
	public void XtGetTcapRequestsRemainByteExact(
		string name,
		string encodedName
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				$"\u001bP+q{encodedName}\u001b\\"
			),
			TerminalXtGetTcapProtocol.CreateRequest( name ).ToArray()
		);
	}

	[Fact]
	public void ConsolidatedRequestsRetainNormalizedDcsStructure() {
		AssertRequestStructure(
			TerminalDecrqssProtocol.CreateRequest(
				TerminalStatusStringKind.CursorStyle
			),
			(byte)'$',
			Encoding.ASCII.GetBytes( " q" )
		);
		AssertRequestStructure(
			TerminalXtGetTcapProtocol.CreateRequest( "TN" ),
			(byte)'+',
			Encoding.ASCII.GetBytes( "544E" )
		);
	}

	private static void AssertRequestStructure(
		ReadOnlyMemory<byte> request,
		byte intermediate,
		byte[] payload
	) {
		ArgumentNullException.ThrowIfNull( payload );

		TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse(
			new TerminalResponseFrame(
				TerminalResponseFrameKind.Dcs,
				request.ToArray()
			)
		);

		Assert.Equal( TerminalControlFamily.Dcs, structure.Family );
		Assert.False( structure.UsesEightBitIntroducer );
		Assert.Empty( structure.ParameterBytes.ToArray() );
		Assert.Equal(
			new byte[] { intermediate },
			structure.IntermediateBytes.ToArray()
		);
		Assert.Equal( (byte)'q', structure.FinalByte );
		Assert.Equal( payload, structure.PayloadBytes.ToArray() );
		Assert.Equal(
			TerminalStringTerminatorKind.SevenBitSt,
			structure.TerminatorKind
		);
	}
}
