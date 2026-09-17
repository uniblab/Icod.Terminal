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

/// <summary>
/// Freezes the T165 terminal-driven Kitty animation playback encoding contract.
/// </summary>
public sealed class KittyGraphicsPersistentAnimationPlaybackEncoderTests {
	[Fact]
	public void StopUsesTerminalAnimationStopState() {
		ReadOnlyMemory<byte> payload =
			KittyGraphicsPersistentAnimationEncoder.EncodeStopPayload( imageId: 99 );

		Assert.Equal(
			"Ga=a,i=99,s=1",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void LoadingRunUsesTerminalAnimationLoadingState() {
		ReadOnlyMemory<byte> payload =
			KittyGraphicsPersistentAnimationEncoder.EncodeRunLoadingPayload( imageId: 99 );

		Assert.Equal(
			"Ga=a,i=99,s=2",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Fact]
	public void NormalRunWithoutFiniteRepeatUsesInfiniteLoopEncoding() {
		ReadOnlyMemory<byte> payload =
			KittyGraphicsPersistentAnimationEncoder.EncodeRunPayload(
				imageId: 99,
				repeatCount: null
			);

		Assert.Equal(
			"Ga=a,i=99,s=3,v=1",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Theory]
	[InlineData( 1, 2 )]
	[InlineData( 2, 3 )]
	[InlineData( 17, 18 )]
	[InlineData( int.MaxValue - 1, int.MaxValue )]
	public void FiniteRepeatCountMapsToOnePlusProtocolLoopValue(
		int repeatCount,
		int expectedProtocolValue
	) {
		ReadOnlyMemory<byte> payload =
			KittyGraphicsPersistentAnimationEncoder.EncodeRunPayload(
				imageId: 99,
				repeatCount
			);

		Assert.Equal(
			$"Ga=a,i=99,s=3,v={expectedProtocolValue}",
			Encoding.ASCII.GetString( payload.Span )
		);
	}

	[Theory]
	[InlineData( 0u )]
	public void PlaybackRejectsZeroImageIdentity(
		uint imageId
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentAnimationEncoder.EncodeStopPayload( imageId )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentAnimationEncoder.EncodeRunLoadingPayload( imageId )
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentAnimationEncoder.EncodeRunPayload(
				imageId,
				repeatCount: null
			)
		);
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( -1 )]
	[InlineData( int.MinValue )]
	[InlineData( int.MaxValue )]
	public void NormalRunRejectsUnrepresentableFiniteRepeatCount(
		int repeatCount
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentAnimationEncoder.EncodeRunPayload(
				imageId: 99,
				repeatCount
			)
		);
	}
}
