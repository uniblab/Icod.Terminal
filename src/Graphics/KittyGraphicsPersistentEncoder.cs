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
/// Encodes the bounded Kitty Graphics control payloads used by persistent
/// raster resource and placement ownership.
/// </summary>
internal static class KittyGraphicsPersistentEncoder {
	internal static IEnumerable<ReadOnlyMemory<byte>> EncodeUploadPayloads(
		KittyRasterData raster,
		uint imageNumber
	) {
		ArgumentNullException.ThrowIfNull( raster );
		ValidateNonZeroIdentity(
			imageNumber,
			nameof( imageNumber ),
			"A persistent Kitty Graphics image number must be non-zero."
		);
		return EncodeUploadPayloadsCore(
			raster,
			imageNumber
		);
	}

	internal static ReadOnlyMemory<byte> EncodePlacementPayload(
		uint imageId,
		uint placementId,
		TerminalRasterPlacementOptions? options
	) {
		ValidateNonZeroIdentity(
			imageId,
			nameof( imageId ),
			"A persistent Kitty Graphics image id must be non-zero."
		);
		ValidateNonZeroIdentity(
			placementId,
			nameof( placementId ),
			"A persistent Kitty Graphics placement id must be non-zero."
		);
		options?.Validate();

		StringBuilder value = new();
		_ = value.Append( "Ga=p,i=" );
		_ = value.Append( imageId.ToString( CultureInfo.InvariantCulture ) );
		_ = value.Append( ",p=" );
		_ = value.Append( placementId.ToString( CultureInfo.InvariantCulture ) );
		_ = value.Append( ",C=1" );
		if ( options?.SourceRectangle is TerminalRasterSourceRectangle rectangle ) {
			_ = value.Append( ",x=" );
			_ = value.Append( rectangle.X.ToString( CultureInfo.InvariantCulture ) );
			_ = value.Append( ",y=" );
			_ = value.Append( rectangle.Y.ToString( CultureInfo.InvariantCulture ) );
			_ = value.Append( ",w=" );
			_ = value.Append( rectangle.Width.ToString( CultureInfo.InvariantCulture ) );
			_ = value.Append( ",h=" );
			_ = value.Append( rectangle.Height.ToString( CultureInfo.InvariantCulture ) );
		}
		if ( options?.Columns is int columns ) {
			_ = value.Append( ",c=" );
			_ = value.Append( columns.ToString( CultureInfo.InvariantCulture ) );
		}
		if ( options?.Rows is int rows ) {
			_ = value.Append( ",r=" );
			_ = value.Append( rows.ToString( CultureInfo.InvariantCulture ) );
		}
		if ( options?.ZIndex is int zIndex ) {
			_ = value.Append( ",z=" );
			_ = value.Append( zIndex.ToString( CultureInfo.InvariantCulture ) );
		}
		return Encoding.ASCII.GetBytes( value.ToString() );
	}

	internal static ReadOnlyMemory<byte> EncodePlacementPayload(
		uint imageId,
		uint placementId,
		int? columns,
		int? rows
	) {
		return EncodePlacementPayload(
			imageId,
			placementId,
			new TerminalRasterPlacementOptions {
				Columns = columns,
				Rows = rows
			}
		);
	}

	internal static ReadOnlyMemory<byte> EncodeDeletePlacementPayload(
		uint imageId,
		uint placementId
	) {
		ValidateNonZeroIdentity(
			imageId,
			nameof( imageId ),
			"A persistent Kitty Graphics image id must be non-zero."
		);
		ValidateNonZeroIdentity(
			placementId,
			nameof( placementId ),
			"A persistent Kitty Graphics placement id must be non-zero."
		);

		return Encoding.ASCII.GetBytes(
			"Ga=d,d=i,i="
			+ imageId.ToString( CultureInfo.InvariantCulture )
			+ ",p="
			+ placementId.ToString( CultureInfo.InvariantCulture )
			+ ",q=2"
		);
	}

	internal static ReadOnlyMemory<byte> EncodeDeleteResourcePayload(
		uint imageId
	) {
		ValidateNonZeroIdentity(
			imageId,
			nameof( imageId ),
			"A persistent Kitty Graphics image id must be non-zero."
		);

		return Encoding.ASCII.GetBytes(
			"Ga=d,d=I,i="
			+ imageId.ToString( CultureInfo.InvariantCulture )
			+ ",q=2"
		);
	}

	private static IEnumerable<ReadOnlyMemory<byte>> EncodeUploadPayloadsCore(
		KittyRasterData raster,
		uint imageNumber
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
					? CreateFirstUploadControlData(
						raster,
						imageNumber,
						isFinal
					)
					: CreateContinuationControlData( isFinal )
			);
			int encodedCount = checked( ( ( rawCount + 2 ) / 3 ) * 4 );
			if ( KittyGraphicsDirectEncoder.MaximumEncodedPayloadBytes < encodedCount ) {
				throw new InvalidOperationException(
					"The persistent Kitty Graphics Base64 chunk exceeded the reviewed payload ceiling."
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
					"The persistent Kitty Graphics Base64 encoder did not consume and encode the complete bounded chunk."
				);
			}

			yield return payload;
			offset = checked( offset + rawCount );
			first = false;
		}
	}

	private static string CreateFirstUploadControlData(
		KittyRasterData raster,
		uint imageNumber,
		bool isFinal
	) {
		ArgumentNullException.ThrowIfNull( raster );
		ValidateNonZeroIdentity(
			imageNumber,
			nameof( imageNumber ),
			"A persistent Kitty Graphics image number must be non-zero."
		);

		return "Ga=t,f="
			+ ( (int)raster.PixelFormat ).ToString( CultureInfo.InvariantCulture )
			+ ",s="
			+ raster.Width.ToString( CultureInfo.InvariantCulture )
			+ ",v="
			+ raster.Height.ToString( CultureInfo.InvariantCulture )
			+ ",t=d,I="
			+ imageNumber.ToString( CultureInfo.InvariantCulture )
			+ ",m="
			+ ( isFinal ? "0" : "1" )
			+ ";";
	}

	private static string CreateContinuationControlData(
		bool isFinal
	) {
		return isFinal
			? "Gm=0;"
			: "Gm=1;"
		;
	}

	private static void ValidatePlacementExtent(
		int? value,
		string parameterName
	) {
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		if ( !value.HasValue ) {
			return;
		}
		if ( value.Value is < 1 or > TerminalRasterImage.MaximumDimension ) {
			throw new ArgumentOutOfRangeException(
				parameterName,
				value,
				$"A persistent raster placement extent must be between 1 and {TerminalRasterImage.MaximumDimension}."
			);
		}
	}

	private static void ValidateNonZeroIdentity(
		uint value,
		string parameterName,
		string message
	) {
		ArgumentException.ThrowIfNullOrEmpty( parameterName );
		ArgumentException.ThrowIfNullOrEmpty( message );
		if ( 0 == value ) {
			throw new ArgumentOutOfRangeException(
				parameterName,
				value,
				message
			);
		}
	}
}
