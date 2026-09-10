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
/// Encodes bounded Kitty Graphics direct-transfer application payloads from one raw raster.
/// </summary>
internal static class KittyGraphicsDirectEncoder {
	internal const int MaximumEncodedPayloadBytes = 4096;
	internal const int MaximumRawChunkBytes = 3 * MaximumEncodedPayloadBytes / 4;

	/// <summary>
	/// Returns a lazy sequence of bounded Kitty Graphics application payloads for one
	/// transmit-and-display operation.
	/// </summary>
	internal static IEnumerable<ReadOnlyMemory<byte>> EncodeDisplayPayloads(
		KittyRasterData raster
	) {
		ArgumentNullException.ThrowIfNull( raster );
		return EncodeDisplayPayloadsCore( raster );
	}

	private static IEnumerable<ReadOnlyMemory<byte>> EncodeDisplayPayloadsCore(
		KittyRasterData raster
	) {
		ReadOnlyMemory<byte> bytes = raster.Bytes;
		int offset = 0;
		bool first = true;
		while ( offset < bytes.Length ) {
			int remaining = bytes.Length - offset;
			int rawCount = Math.Min(
				MaximumRawChunkBytes,
				remaining
			);
			bool isFinal = rawCount == remaining;
			byte[] controlData = Encoding.ASCII.GetBytes(
				first
					? CreateFirstControlData( raster, isFinal )
					: CreateContinuationControlData( isFinal )
			);
			int encodedCount = checked( ( ( rawCount + 2 ) / 3 ) * 4 );
			if ( MaximumEncodedPayloadBytes < encodedCount ) {
				throw new InvalidOperationException(
					"The Kitty Graphics Base64 chunk exceeded the reviewed payload ceiling."
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
					"The Kitty Graphics Base64 encoder did not consume and encode the complete bounded chunk."
				);
			}

			yield return payload;
			offset = checked( offset + rawCount );
			first = false;
		}
	}

	private static string CreateFirstControlData(
		KittyRasterData raster,
		bool isFinal
	) {
		ArgumentNullException.ThrowIfNull( raster );

		return "Ga=T,f="
			+ ( (int)raster.PixelFormat ).ToString( CultureInfo.InvariantCulture )
			+ ",s="
			+ raster.Width.ToString( CultureInfo.InvariantCulture )
			+ ",v="
			+ raster.Height.ToString( CultureInfo.InvariantCulture )
			+ ",t=d,m="
			+ ( isFinal ? "0" : "1" )
			+ ",q=2;";
	}

	private static string CreateContinuationControlData(
		bool isFinal
	) {
		return isFinal
			? "Gm=0,q=2;"
			: "Gm=1,q=2;"
		;
	}
}
