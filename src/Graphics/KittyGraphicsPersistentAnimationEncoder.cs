/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

/// <summary>
/// Encodes bounded full-frame Kitty Graphics animation transfers and controls for an existing
/// persistent raster resource.
/// </summary>
internal static class KittyGraphicsPersistentAnimationEncoder {
	internal static IEnumerable<ReadOnlyMemory<byte>> EncodeFramePayloads(
		KittyRasterData raster,
		uint imageId,
		int gapMilliseconds
	) {
		ArgumentNullException.ThrowIfNull( raster );
		ValidateImageId( imageId );
		ValidateGapMilliseconds( gapMilliseconds );

		return EncodeFramePayloadsCore(
			raster,
			imageId,
			gapMilliseconds
		);
	}

	internal static ReadOnlyMemory<byte> EncodeFrameDurationPayload(
		uint imageId,
		uint frameNumber,
		int gapMilliseconds
	) {
		ValidateImageId( imageId );
		ValidateFrameNumber( frameNumber );
		ValidateGapMilliseconds( gapMilliseconds );

		return Encoding.ASCII.GetBytes(
			"Ga=a,i="
			+ imageId.ToString( CultureInfo.InvariantCulture )
			+ ",r="
			+ frameNumber.ToString( CultureInfo.InvariantCulture )
			+ ",z="
			+ gapMilliseconds.ToString( CultureInfo.InvariantCulture )
		);
	}

	internal static ReadOnlyMemory<byte> EncodeCurrentFramePayload(
		uint imageId,
		uint frameNumber
	) {
		ValidateImageId( imageId );
		ValidateFrameNumber( frameNumber );

		return Encoding.ASCII.GetBytes(
			"Ga=a,i="
			+ imageId.ToString( CultureInfo.InvariantCulture )
			+ ",c="
			+ frameNumber.ToString( CultureInfo.InvariantCulture )
		);
	}

	internal static ReadOnlyMemory<byte> EncodeStopPayload(
		uint imageId
	) {
		return EncodePlaybackStatePayload(
			imageId,
			state: 1
		);
	}

	internal static ReadOnlyMemory<byte> EncodeRunLoadingPayload(
		uint imageId
	) {
		return EncodePlaybackStatePayload(
			imageId,
			state: 2
		);
	}

	internal static ReadOnlyMemory<byte> EncodeRunPayload(
		uint imageId,
		int? repeatCount
	) {
		ValidateImageId( imageId );
		int protocolLoopCount;
		if ( repeatCount.HasValue ) {
			if ( repeatCount.Value < 1 || int.MaxValue == repeatCount.Value ) {
				throw new ArgumentOutOfRangeException(
					nameof( repeatCount ),
					repeatCount,
					$"A finite animation repeat count must be between 1 and {int.MaxValue - 1}."
				);
			}
			protocolLoopCount = checked( repeatCount.Value + 1 );
		} else {
			protocolLoopCount = 1;
		}

		return Encoding.ASCII.GetBytes(
			"Ga=a,i="
			+ imageId.ToString( CultureInfo.InvariantCulture )
			+ ",s=3,v="
			+ protocolLoopCount.ToString( CultureInfo.InvariantCulture )
		);
	}

	private static IEnumerable<ReadOnlyMemory<byte>> EncodeFramePayloadsCore(
		KittyRasterData raster,
		uint imageId,
		int gapMilliseconds
	) {
		ReadOnlyMemory<byte> bytes = raster.Bytes;
		int offset = 0;
		bool first = true;
		while ( offset < bytes.Length ) {
			int remaining = bytes.Length - offset;
			int rawCount = Math.Min(
				KittyGraphicsDirectEncoder.MaximumRawChunkBytes,
				remaining
			);
			bool isFinal = rawCount == remaining;
			byte[] controlData = Encoding.ASCII.GetBytes(
				first
					? CreateFirstControlData(
						raster,
						imageId,
						gapMilliseconds,
						isFinal
					)
					: CreateContinuationControlData( isFinal )
			);
			int encodedCount = checked( ( ( rawCount + 2 ) / 3 ) * 4 );
			if ( KittyGraphicsDirectEncoder.MaximumEncodedPayloadBytes < encodedCount ) {
				throw new InvalidOperationException(
					"The persistent Kitty Graphics animation Base64 chunk exceeded the reviewed payload ceiling."
				);
			}

			byte[] payload = new byte[
				checked( controlData.Length + encodedCount )
			];
			controlData.CopyTo(
				payload,
				0
			);
			OperationStatus status = Base64.EncodeToUtf8(
				bytes.Span.Slice(
					offset,
					rawCount
				),
				payload.AsSpan( controlData.Length ),
				out int consumed,
				out int written,
				isFinalBlock: true
			);
			if ( OperationStatus.Done != status
				|| rawCount != consumed
				|| encodedCount != written ) {
				throw new InvalidOperationException(
					"The persistent Kitty Graphics animation Base64 encoder did not consume and encode the complete bounded chunk."
				);
			}

			yield return payload;
			offset = checked( offset + rawCount );
			first = false;
		}
	}

	private static ReadOnlyMemory<byte> EncodePlaybackStatePayload(
		uint imageId,
		int state
	) {
		ValidateImageId( imageId );
		if ( state is < 1 or > 2 ) {
			throw new ArgumentOutOfRangeException( nameof( state ) );
		}

		return Encoding.ASCII.GetBytes(
			"Ga=a,i="
			+ imageId.ToString( CultureInfo.InvariantCulture )
			+ ",s="
			+ state.ToString( CultureInfo.InvariantCulture )
		);
	}

	private static string CreateFirstControlData(
		KittyRasterData raster,
		uint imageId,
		int gapMilliseconds,
		bool isFinal
	) {
		return "Ga=f,f="
			+ ( (int)raster.PixelFormat ).ToString( CultureInfo.InvariantCulture )
			+ ",s="
			+ raster.Width.ToString( CultureInfo.InvariantCulture )
			+ ",v="
			+ raster.Height.ToString( CultureInfo.InvariantCulture )
			+ ",t=d,i="
			+ imageId.ToString( CultureInfo.InvariantCulture )
			+ ",z="
			+ gapMilliseconds.ToString( CultureInfo.InvariantCulture )
			+ ",m="
			+ ( isFinal ? "0" : "1" )
			+ ";";
	}

	private static string CreateContinuationControlData(
		bool isFinal
	) {
		return isFinal
			? "Ga=f,m=0;"
			: "Ga=f,m=1;"
		;
	}

	private static void ValidateImageId(
		uint imageId
	) {
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A persistent Kitty Graphics animation image id must be non-zero."
			);
		}
	}

	private static void ValidateFrameNumber(
		uint frameNumber
	) {
		if ( 0u == frameNumber ) {
			throw new ArgumentOutOfRangeException(
				nameof( frameNumber ),
				frameNumber,
				"A persistent Kitty Graphics animation frame number must be non-zero."
			);
		}
	}

	private static void ValidateGapMilliseconds(
		int gapMilliseconds
	) {
		if ( gapMilliseconds <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( gapMilliseconds ),
				gapMilliseconds,
				"A persistent Kitty Graphics animation frame gap must be positive."
			);
		}
	}
}
