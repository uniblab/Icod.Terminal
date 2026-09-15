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
/// Encodes bounded full-frame Kitty Graphics animation transfers for an existing
/// persistent raster resource.
/// </summary>
internal static class KittyGraphicsPersistentAnimationEncoder {
	internal static IEnumerable<ReadOnlyMemory<byte>> EncodeFramePayloads(
		KittyRasterData raster,
		uint imageId,
		int gapMilliseconds
	) {
		ArgumentNullException.ThrowIfNull( raster );
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A persistent Kitty Graphics animation image id must be non-zero."
			);
		}
		if ( gapMilliseconds <= 0 ) {
			throw new ArgumentOutOfRangeException(
				nameof( gapMilliseconds ),
				gapMilliseconds,
				"A persistent Kitty Graphics animation frame gap must be positive."
			);
		}

		return EncodeFramePayloadsCore(
			raster,
			imageId,
			gapMilliseconds
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
}
