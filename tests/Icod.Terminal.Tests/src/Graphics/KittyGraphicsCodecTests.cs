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
/// Verifies the A181 Kitty Graphics control-data and response grammar.
/// </summary>
public sealed class KittyGraphicsCodecTests {
	[Fact]
	public void SupportQueryMatchesDocumentedOnePixelVector() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA"
			),
			KittyGraphicsCodec.EncodeSupportQueryPayload( 31 )
		);
	}

	[Fact]
	public void SupportQueryAcceptsMaximumUInt32Identity() {
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"Gi=4294967295,s=1,v=1,a=q,t=d,f=24;AAAA"
			),
			KittyGraphicsCodec.EncodeSupportQueryPayload( uint.MaxValue )
		);
	}

	[Fact]
	public void SupportQueryRejectsZeroIdentity() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsCodec.EncodeSupportQueryPayload( 0 )
		);
	}

	[Fact]
	public void SupportQueryComposesIntoCanonicalApcFrame() {
		byte[] frame = ApcWriter.EncodeFrame(
			KittyGraphicsCodec.EncodeSupportQueryPayload( 31 )
		);

		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=31,s=1,v=1,a=q,t=d,f=24;AAAA\u001b\\"
			),
			frame
		);
	}

	[Fact]
	public void SevenBitSuccessResponseParses() {
		KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse(
			CreateSevenBitResponse( "Gi=31;OK" )
		);

		Assert.Equal( (uint)31, response.ImageId );
		Assert.Null( response.ImageNumber );
		Assert.Null( response.PlacementId );
		Assert.True( response.IsSuccess );
		Assert.Equal( "OK", response.Message );
	}

	[Fact]
	public void EightBitApcAndStringTerminatorResponseParses() {
		byte[] payload = Encoding.ASCII.GetBytes( "Gi=31;OK" );
		byte[] frameBytes = new byte[ payload.Length + 2 ];
		frameBytes[ 0 ] = 0x9F;
		payload.CopyTo(
			frameBytes,
			1
		);
		frameBytes[ ^1 ] = 0x9C;

		KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse(
			new TerminalResponseFrame(
				TerminalResponseFrameKind.Apc,
				frameBytes
			)
		);

		Assert.Equal( (uint)31, response.ImageId );
		Assert.True( response.IsSuccess );
	}

	[Fact]
	public void PrintableProtocolErrorIsRetainedWithoutPrematureTaxonomy() {
		KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse(
			CreateSevenBitResponse(
				"Gi=31;ENOENT: image not found"
			)
		);

		Assert.False( response.IsSuccess );
		Assert.Equal(
			"ENOENT: image not found",
			response.Message
		);
	}

	[Fact]
	public void OptionalImageNumberAndPlacementIdentityAreParsed() {
		KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse(
			CreateSevenBitResponse(
				"Gi=99,I=13,p=7;OK"
			)
		);

		Assert.Equal( (uint)99, response.ImageId );
		Assert.Equal( (uint)13, response.ImageNumber );
		Assert.Equal( (uint)7, response.PlacementId );
	}

	[Fact]
	public void StructurallyValidUnknownControlKeyIsIgnored() {
		KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse(
			CreateSevenBitResponse(
				"Gi=31,x=future;OK"
			)
		);

		Assert.Equal( (uint)31, response.ImageId );
		Assert.True( response.IsSuccess );
	}

	[Theory]
	[InlineData( "Gi=31,i=32;OK" )]
	[InlineData( "Gi=31,x=one,x=two;OK" )]
	public void DuplicateControlKeysAreRejected(
		string payload
	) {
		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse( payload )
			)
		);
	}

	[Theory]
	[InlineData( "GI=13;OK" )]
	[InlineData( "Gp=7;OK" )]
	public void CorrelatedResponseRequiresImageId(
		string payload
	) {
		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse( payload )
			)
		);
	}

	[Fact]
	public void CorrelatedResponseRejectsZeroImageId() {
		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse( "Gi=0;OK" )
			)
		);
	}

	[Fact]
	public void CorrelatedResponseAcceptsMaximumUInt32ImageId() {
		KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse(
			CreateSevenBitResponse(
				"Gi=4294967295;OK"
			)
		);

		Assert.Equal( uint.MaxValue, response.ImageId );
	}

	[Theory]
	[InlineData( "Gi=4294967296;OK" )]
	[InlineData( "Gi=+31;OK" )]
	[InlineData( "Gi=-31;OK" )]
	[InlineData( "Gi=0x1f;OK" )]
	[InlineData( "Gi=31 ;OK" )]
	public void ImageIdRequiresBoundedUnsignedDecimalSyntax(
		string payload
	) {
		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse( payload )
			)
		);
	}

	[Theory]
	[InlineData( "Gii=31;OK" )]
	[InlineData( "Gi=;OK" )]
	[InlineData( "Gi=31,,p=1;OK" )]
	[InlineData( "G1=31;OK" )]
	[InlineData( "Gi=31,;OK" )]
	[InlineData( "G;OK" )]
	public void MalformedControlDataIsRejected(
		string payload
	) {
		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse( payload )
			)
		);
	}

	[Fact]
	public void EmptyResponseMessageIsRejected() {
		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse( "Gi=31;" )
			)
		);
	}

	[Theory]
	[InlineData( 0x00 )]
	[InlineData( 0x1B )]
	[InlineData( 0x7F )]
	[InlineData( 0x80 )]
	public void ResponseMessageMustBePrintableAscii(
		int value
	) {
		byte[] prefix = Encoding.ASCII.GetBytes( "Gi=31;" );
		byte[] payload = new byte[ prefix.Length + 1 ];
		prefix.CopyTo(
			payload,
			0
		);
		payload[ ^1 ] = checked( (byte)value );

		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse( payload )
			)
		);
	}

	[Fact]
	public void ResponseMessageAtDialectCeilingIsAccepted() {
		string message = new(
			'E',
			KittyGraphicsCodec.MaximumResponseMessageBytes
		);
		KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse(
			CreateSevenBitResponse(
				"Gi=31;" + message
			)
		);

		Assert.False( response.IsSuccess );
		Assert.Equal( message, response.Message );
	}

	[Fact]
	public void ResponseMessageAboveDialectCeilingIsRejected() {
		string message = new(
			'E',
			KittyGraphicsCodec.MaximumResponseMessageBytes + 1
		);

		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse(
					"Gi=31;" + message
				)
			)
		);
	}

	[Fact]
	public void OversizedControlDataIsRejectedBeforeFieldParsing() {
		string control = new(
			'x',
			KittyGraphicsCodec.MaximumControlDataBytes + 1
		);

		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse(
					"G" + control + ";OK"
				)
			)
		);
	}

	[Fact]
	public void NonApcFrameIsRejected() {
		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				new TerminalResponseFrame(
					TerminalResponseFrameKind.Csi,
					Encoding.ASCII.GetBytes( "\u001b[?1;2c" )
				)
			)
		);
	}

	[Fact]
	public void NonKittyApcFrameIsRejected() {
		Assert.Throws<FormatException>(
			() => KittyGraphicsCodec.ParseResponse(
				CreateSevenBitResponse( "Xi=31;OK" )
			)
		);
	}

	private static TerminalResponseFrame CreateSevenBitResponse(
		string payload
	) {
		ArgumentNullException.ThrowIfNull( payload );
		return CreateSevenBitResponse(
			Encoding.ASCII.GetBytes( payload )
		);
	}

	private static TerminalResponseFrame CreateSevenBitResponse(
		byte[] payload
	) {
		ArgumentNullException.ThrowIfNull( payload );
		byte[] frameBytes = new byte[ payload.Length + 4 ];
		frameBytes[ 0 ] = 0x1B;
		frameBytes[ 1 ] = (byte)'_';
		payload.CopyTo(
			frameBytes,
			2
		);
		frameBytes[ ^2 ] = 0x1B;
		frameBytes[ ^1 ] = (byte)'\\';
		return new TerminalResponseFrame(
			TerminalResponseFrameKind.Apc,
			frameBytes
		);
	}
}
