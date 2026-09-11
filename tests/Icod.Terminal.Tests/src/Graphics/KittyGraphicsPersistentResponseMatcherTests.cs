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
/// Defines C111 image-number acknowledgement correlation for persistent Kitty Graphics.
/// </summary>
public sealed class KittyGraphicsPersistentResponseMatcherTests {
	[Fact]
	public void MatcherOwnsApcResponsesForExpectedImageNumber() {
		KittyGraphicsPersistentResponseMatcher matcher = new( 31 );
		TerminalResponseFrame frame = CreateApcResponse(
			"Gi=99,I=31;OK"
		);

		Assert.Equal( TerminalResponseFrameKind.Apc, matcher.FrameKind );
		Assert.True( matcher.IsMatch( frame ) );
	}

	[Fact]
	public void MatcherRejectsDifferentImageNumber() {
		KittyGraphicsPersistentResponseMatcher matcher = new( 31 );
		TerminalResponseFrame frame = CreateApcResponse(
			"Gi=99,I=32;OK"
		);

		Assert.False( matcher.IsMatch( frame ) );
	}

	[Fact]
	public void MatcherRejectsResponseWithoutImageNumber() {
		KittyGraphicsPersistentResponseMatcher matcher = new( 31 );
		TerminalResponseFrame frame = CreateApcResponse(
			"Gi=99;OK"
		);

		Assert.False( matcher.IsMatch( frame ) );
	}

	[Fact]
	public void CorrelatedPrefixRequiresCompleteExpectedImageNumberField() {
		KittyGraphicsPersistentResponseMatcher matcher = new( 31 );

		Assert.True(
			matcher.IsCorrelatedPrefix(
				Encoding.ASCII.GetBytes( "\u001b_Gi=99,I=31;" )
			)
		);
		Assert.False(
			matcher.IsCorrelatedPrefix(
				Encoding.ASCII.GetBytes( "\u001b_Gi=99,I=3" )
			)
		);
		Assert.False(
			matcher.IsCorrelatedPrefix(
				Encoding.ASCII.GetBytes( "\u001b_Gi=99,I=32;" )
			)
		);
	}

	[Fact]
	public void ConstructorRejectsZeroImageNumber() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => new KittyGraphicsPersistentResponseMatcher( 0 )
		);
	}

	[Fact]
	public void DuplicateImageNumberIsMalformedWhenCorrelated() {
		KittyGraphicsPersistentResponseMatcher matcher = new( 31 );
		TerminalResponseFrame frame = CreateApcResponse(
			"Gi=99,I=31,I=31;OK"
		);

		Assert.Throws<FormatException>(
			() => matcher.IsMatch( frame )
		);
	}

	[Fact]
	public void OverflowingAssignedImageIdIsMalformedWhenCorrelated() {
		KittyGraphicsPersistentResponseMatcher matcher = new( 31 );
		TerminalResponseFrame frame = CreateApcResponse(
			"Gi=4294967296,I=31;OK"
		);

		Assert.Throws<FormatException>(
			() => matcher.IsMatch( frame )
		);
	}

	private static TerminalResponseFrame CreateApcResponse(
		string payload
	) {
		ArgumentException.ThrowIfNullOrEmpty( payload );
		return new TerminalResponseFrame(
			TerminalResponseFrameKind.Apc,
			ApcWriter.EncodeFrame(
				Encoding.ASCII.GetBytes( payload )
			)
		);
	}
}
