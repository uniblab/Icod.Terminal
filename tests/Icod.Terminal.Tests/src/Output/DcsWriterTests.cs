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
/// Verifies the D170 canonical small-frame DCS construction substrate.
/// </summary>
public sealed class DcsWriterTests {
	[Fact]
	public void EmptyStructuralFieldsUseCanonicalSevenBitFraming() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[Pq\u001b\\" ),
			DcsWriter.EncodeFrame(
				ReadOnlySpan<byte>.Empty,
				ReadOnlySpan<byte>.Empty,
				(byte)'q',
				ReadOnlySpan<byte>.Empty
			)
		);
	}

	[Fact]
	public void StructuralFieldsAndPayloadRemainByteExact() {
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[P1;2$qabc\u001b\\" ),
			DcsWriter.EncodeFrame(
				Encoding.ASCII.GetBytes( "1;2" ),
				[ (byte)'$' ],
				(byte)'q',
				Encoding.ASCII.GetBytes( "abc" )
			)
		);
	}

	[Theory]
	[InlineData( 0x2F )]
	[InlineData( 0x40 )]
	public void InvalidParameterBytesAreRejected(
		int value
	) {
		byte[] parameterBytes = [ checked( (byte)value ) ];
		Assert.Throws<ArgumentException>(
			() => DcsWriter.EncodeFrame(
				parameterBytes,
				ReadOnlySpan<byte>.Empty,
				(byte)'q',
				ReadOnlySpan<byte>.Empty
			)
		);
	}

	[Theory]
	[InlineData( 0x1F )]
	[InlineData( 0x30 )]
	public void InvalidIntermediateBytesAreRejected(
		int value
	) {
		byte[] intermediateBytes = [ checked( (byte)value ) ];
		Assert.Throws<ArgumentException>(
			() => DcsWriter.EncodeFrame(
				ReadOnlySpan<byte>.Empty,
				intermediateBytes,
				(byte)'q',
				ReadOnlySpan<byte>.Empty
			)
		);
	}

	[Theory]
	[InlineData( 0x3F )]
	[InlineData( 0x7F )]
	public void InvalidFinalSelectorsAreRejected(
		int value
	) {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => DcsWriter.EncodeFrame(
				ReadOnlySpan<byte>.Empty,
				ReadOnlySpan<byte>.Empty,
				checked( (byte)value ),
				ReadOnlySpan<byte>.Empty
			)
		);
	}

	[Theory]
	[InlineData( 0x18 )]
	[InlineData( 0x1A )]
	[InlineData( 0x1B )]
	[InlineData( 0x9C )]
	public void PayloadCannotContainFramingOrAbortControls(
		int value
	) {
		byte[] payload = [ (byte)'a', checked( (byte)value ), (byte)'b' ];
		Assert.Throws<ArgumentException>(
			() => DcsWriter.EncodeFrame(
				ReadOnlySpan<byte>.Empty,
				ReadOnlySpan<byte>.Empty,
				(byte)'q',
				payload
			)
		);
	}

	[Fact]
	public void ExactMaximumSmallFrameSizeIsAccepted() {
		byte[] payload = Enumerable.Repeat(
			(byte)'A',
			DcsWriter.MaximumEncodedFrameBytes - 5
		).ToArray();

		byte[] frame = DcsWriter.EncodeFrame(
			ReadOnlySpan<byte>.Empty,
			ReadOnlySpan<byte>.Empty,
			(byte)'q',
			payload
		);

		Assert.Equal( DcsWriter.MaximumEncodedFrameBytes, frame.Length );
		Assert.Equal( 0x1B, frame[ 0 ] );
		Assert.Equal( (byte)'P', frame[ 1 ] );
		Assert.Equal( (byte)'q', frame[ 2 ] );
		Assert.Equal( 0x1B, frame[ ^2 ] );
		Assert.Equal( (byte)'\\', frame[ ^1 ] );
	}

	[Fact]
	public void MaximumSmallFrameSizePlusOneIsRejected() {
		byte[] payload = Enumerable.Repeat(
			(byte)'A',
			DcsWriter.MaximumEncodedFrameBytes - 4
		).ToArray();

		Assert.Throws<ArgumentOutOfRangeException>(
			() => DcsWriter.EncodeFrame(
				ReadOnlySpan<byte>.Empty,
				ReadOnlySpan<byte>.Empty,
				(byte)'q',
				payload
			)
		);
	}
}
