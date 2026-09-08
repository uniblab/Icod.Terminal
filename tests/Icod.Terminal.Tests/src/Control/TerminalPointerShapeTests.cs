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

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies the frozen 0.11 semantic pointer-shape vocabulary and canonical OSC 22 names.
/// </summary>
public sealed class TerminalPointerShapeTests {
	public static TheoryData<TerminalPointerShape, string> CanonicalShapes {
		get {
			return new TheoryData<TerminalPointerShape, string> {
				{ TerminalPointerShape.Alias, "alias" },
				{ TerminalPointerShape.Cell, "cell" },
				{ TerminalPointerShape.Copy, "copy" },
				{ TerminalPointerShape.Crosshair, "crosshair" },
				{ TerminalPointerShape.Default, "default" },
				{ TerminalPointerShape.EastResize, "e-resize" },
				{ TerminalPointerShape.EastWestResize, "ew-resize" },
				{ TerminalPointerShape.Grab, "grab" },
				{ TerminalPointerShape.Grabbing, "grabbing" },
				{ TerminalPointerShape.Help, "help" },
				{ TerminalPointerShape.Move, "move" },
				{ TerminalPointerShape.NorthResize, "n-resize" },
				{ TerminalPointerShape.NorthEastResize, "ne-resize" },
				{ TerminalPointerShape.NorthEastSouthWestResize, "nesw-resize" },
				{ TerminalPointerShape.NoDrop, "no-drop" },
				{ TerminalPointerShape.NotAllowed, "not-allowed" },
				{ TerminalPointerShape.NorthSouthResize, "ns-resize" },
				{ TerminalPointerShape.NorthWestResize, "nw-resize" },
				{ TerminalPointerShape.NorthWestSouthEastResize, "nwse-resize" },
				{ TerminalPointerShape.Pointer, "pointer" },
				{ TerminalPointerShape.Progress, "progress" },
				{ TerminalPointerShape.SouthResize, "s-resize" },
				{ TerminalPointerShape.SouthEastResize, "se-resize" },
				{ TerminalPointerShape.SouthWestResize, "sw-resize" },
				{ TerminalPointerShape.Text, "text" },
				{ TerminalPointerShape.VerticalText, "vertical-text" },
				{ TerminalPointerShape.WestResize, "w-resize" },
				{ TerminalPointerShape.Wait, "wait" },
				{ TerminalPointerShape.ZoomIn, "zoom-in" },
				{ TerminalPointerShape.ZoomOut, "zoom-out" }
			};
		}
	}

	[Theory]
	[MemberData( nameof( CanonicalShapes ) )]
	public void EverySemanticShapeMapsToAndFromOneCanonicalName(
		TerminalPointerShape shape,
		string wireName
	) {
		Assert.Equal(
			wireName,
			TerminalPointerShapeCodec.GetWireName( shape )
		);
		Assert.Equal(
			shape,
			TerminalPointerShapeCodec.ParseWireName( wireName )
		);
	}

	[Fact]
	public void FrozenSemanticVocabularyContainsExactlyThirtyShapes() {
		Assert.Equal(
			30,
			Enum.GetValues<TerminalPointerShape>().Length
		);
	}

	[Fact]
	public void DefaultSemanticShapeIsNotTerminalPolicyReset() {
		Assert.Equal(
			"default",
			TerminalPointerShapeCodec.GetWireName( TerminalPointerShape.Default )
		);
		Assert.NotEqual(
			OscWriter.EncodeOsc22PointerShapeFrame( null ),
			OscWriter.EncodeOsc22PointerShapeFrame(
				TerminalPointerShapeCodec.GetWireName( TerminalPointerShape.Default )
			)
		);
	}

	[Theory]
	[InlineData( "" )]
	[InlineData( "Pointer" )]
	[InlineData( "left_ptr" )]
	[InlineData( "hand2" )]
	[InlineData( "pointer,text" )]
	[InlineData( ">pointer" )]
	[InlineData( "?pointer" )]
	[InlineData( "__current__" )]
	public void ParseRejectsNoncanonicalNames(
		string wireName
	) {
		Assert.Throws<FormatException>(
			() => TerminalPointerShapeCodec.ParseWireName( wireName )
		);
	}

	[Fact]
	public void ParseRejectsNull() {
		Assert.Throws<ArgumentNullException>(
			() => TerminalPointerShapeCodec.ParseWireName( null! )
		);
	}

	[Fact]
	public void GetWireNameRejectsUndefinedSemanticValue() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => TerminalPointerShapeCodec.GetWireName(
				(TerminalPointerShape)int.MaxValue
			)
		);
	}
}
