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
/// Verifies C162 canonical CSI construction for existing hard-coded protocol paths.
/// </summary>
public sealed class CsiWriterConsolidationTests {
	[Theory]
	[InlineData( 1000, true, "\u001b[?1000h" )]
	[InlineData( 1000, false, "\u001b[?1000l" )]
	[InlineData( 1002, true, "\u001b[?1002h" )]
	[InlineData( 1003, true, "\u001b[?1003h" )]
	[InlineData( 1006, true, "\u001b[?1006h" )]
	[InlineData( 2026, true, "\u001b[?2026h" )]
	[InlineData( 2026, false, "\u001b[?2026l" )]
	public void DecPrivateModeFramesRemainByteExact(
		int mode,
		bool enabled,
		string expected
	) {
		Assert.Equal(
			Encoding.ASCII.GetBytes( expected ),
			CsiWriter.EncodeDecPrivateModeFrame(
				mode,
				enabled
			)
		);
		Assert.Equal(
			expected,
			CsiWriter.EncodeDecPrivateModeString(
				mode,
				enabled
			)
		);
	}

	[Fact]
	public void KittyKeyboardFramesRemainByteExact() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[?u" ),
			CsiWriter.EncodeKittyKeyboardQueryFrame()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[>31u" ),
			CsiWriter.EncodeKittyKeyboardPushFrame( 31 )
		);
		Assert.Equal(
			"\u001b[>31u",
			CsiWriter.EncodeKittyKeyboardPushString( 31 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[<u" ),
			CsiWriter.EncodeKittyKeyboardPopFrame()
		);
		Assert.Equal(
			"\u001b[<u",
			CsiWriter.EncodeKittyKeyboardPopString()
		);
	}

	[Fact]
	public void ExistingQueryRequestsRemainByteExactThroughCanonicalWriter() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[c" ),
			TerminalCsiQueryProtocol.PrimaryDeviceAttributesRequest.ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[>c" ),
			TerminalCsiQueryProtocol.SecondaryDeviceAttributesRequest.ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[5n" ),
			TerminalCsiQueryProtocol.DeviceStatusRequest.ToArray()
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[6n" ),
			TerminalCsiQueryProtocol.CursorPositionRequest.ToArray()
		);
	}

	[Fact]
	public void PrivateModeAndKittyFlagsRejectNegativeValues() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => CsiWriter.EncodeDecPrivateModeFrame(
				0,
				enabled: true
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => CsiWriter.EncodeKittyKeyboardPushFrame( -1 )
		);
	}
}
