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
namespace Icod.Terminal.Tests.Graphics;

using System.Text;
using Icod.Terminal;
using Xunit;

public sealed class KittyGraphicsPersistentAnimationCompositionEncoderTests {
	[Theory]
	[InlineData( TerminalRasterFrameCompositionMode.AlphaBlend,
		"Ga=c,i=77,r=2,c=3,w=4,h=5,X=6,Y=7,x=8,y=9" )]
	[InlineData( TerminalRasterFrameCompositionMode.Replace,
		"Ga=c,i=77,r=2,c=3,w=4,h=5,X=6,Y=7,x=8,y=9,C=1" )]
	public void CompositionEncodesPrivateFrameIdentityAndPixelGeometry(
		TerminalRasterFrameCompositionMode mode,
		string expected
	) {
		ReadOnlyMemory<byte> payload =
			KittyGraphicsPersistentAnimationEncoder.EncodeCompositionPayload(
				imageId: 77,
				sourceFrameNumber: 2,
				destinationFrameNumber: 3,
				new TerminalRasterSourceRectangle( 6, 7, 4, 5 ),
				destinationX: 8,
				destinationY: 9,
				mode
			);

		Assert.Equal( expected, Encoding.ASCII.GetString( payload.Span ) );
	}

	[Fact]
	public void CompositionRejectsInvalidPrivateIdentityAndMode() {
		TerminalRasterSourceRectangle rectangle = new( 0, 0, 1, 1 );
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentAnimationEncoder.EncodeCompositionPayload(
				0, 1, 2, rectangle, 0, 0,
				TerminalRasterFrameCompositionMode.AlphaBlend
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentAnimationEncoder.EncodeCompositionPayload(
				77, 0, 2, rectangle, 0, 0,
				TerminalRasterFrameCompositionMode.AlphaBlend
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentAnimationEncoder.EncodeCompositionPayload(
				77, 1, 2, rectangle, 0, 0,
				(TerminalRasterFrameCompositionMode)3
			)
		);
	}
}
