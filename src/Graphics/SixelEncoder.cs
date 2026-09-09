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

/// <summary>
/// Encodes one D174 palette image as deterministic bounded Sixel payload segments.
/// </summary>
internal static class SixelEncoder {
	private static readonly ReadOnlyMemory<byte> GraphicsCarriageReturnSegment =
		new byte[] { SixelCodec.GraphicsCarriageReturn };
	private static readonly ReadOnlyMemory<byte> GraphicsNewLineSegment =
		new byte[] { SixelCodec.GraphicsNewLine };

	internal static IEnumerable<ReadOnlyMemory<byte>> EncodePayloadSegments(
		SixelPaletteImage image
	) {
		ArgumentNullException.ThrowIfNull( image );
		return EncodePayloadSegmentsCore( image );
	}

	internal static int ConvertRgb8ToPercentage(
		byte channel
	) {
		return ( channel * 100 + 127 ) / 255;
	}

	private static IEnumerable<ReadOnlyMemory<byte>> EncodePayloadSegmentsCore(
		SixelPaletteImage image
	) {
		TerminalRasterColor[] palette = image.Palette.ToArray();
		yield return SixelCodec.EncodeRasterAttributes(
			image.Width,
			image.Height
		);

		bool[] globallyUsedRegisters = GetGloballyUsedRegisters( image );
		for ( int register = 0; register < globallyUsedRegisters.Length; register++ ) {
			if ( !globallyUsedRegisters[ register ] ) {
				continue;
			}

			TerminalRasterColor color = palette[ register ];
			yield return SixelCodec.EncodeRgbColorDefinition(
				register,
				ConvertRgb8ToPercentage( color.Red ),
				ConvertRgb8ToPercentage( color.Green ),
				ConvertRgb8ToPercentage( color.Blue )
			);
		}

		int paletteCount = palette.Length;
		byte[] bandMasks = new byte[ checked( paletteCount * image.Width ) ];
		bool[] bandUsedRegisters = new bool[ paletteCount ];
		int bandCount = checked( ( image.Height + 5 ) / 6 );
		for ( int band = 0; band < bandCount; band++ ) {
			if ( 0 != bandMasks.Length ) {
				Array.Clear(
					bandMasks,
					0,
					bandMasks.Length
				);
			}
			if ( 0 != bandUsedRegisters.Length ) {
				Array.Clear(
					bandUsedRegisters,
					0,
					bandUsedRegisters.Length
				);
			}

			PopulateBandMasks(
				image,
				band,
				bandMasks,
				bandUsedRegisters
			);

			bool emittedColorPass = false;
			for ( int register = 0; register < paletteCount; register++ ) {
				if ( !bandUsedRegisters[ register ] ) {
					continue;
				}

				if ( emittedColorPass ) {
					yield return GraphicsCarriageReturnSegment;
				}
				yield return EncodeColorPass(
					register,
					bandMasks,
					checked( register * image.Width ),
					image.Width
				);
				emittedColorPass = true;
			}

			if ( band + 1 < bandCount ) {
				yield return GraphicsNewLineSegment;
			}
		}
	}

	private static bool[] GetGloballyUsedRegisters(
		SixelPaletteImage image
	) {
		ArgumentNullException.ThrowIfNull( image );

		int paletteCount = image.Palette.Length;
		bool[] result = new bool[ paletteCount ];
		ReadOnlySpan<byte> indices = image.Indices.Span;
		ReadOnlySpan<byte> transparency = image.TransparentMask.Span;
		for ( int pixelIndex = 0; pixelIndex < image.PixelCount; pixelIndex++ ) {
			if ( 0 != transparency.Length && 0 != transparency[ pixelIndex ] ) {
				continue;
			}

			int register = indices[ pixelIndex ];
			if ( paletteCount <= register ) {
				throw new InvalidOperationException(
					"A Sixel palette image contains an opaque pixel whose palette index is not defined."
				);
			}
			result[ register ] = true;
		}

		return result;
	}

	private static void PopulateBandMasks(
		SixelPaletteImage image,
		int band,
		Span<byte> bandMasks,
		Span<bool> bandUsedRegisters
	) {
		ArgumentNullException.ThrowIfNull( image );
		int bandCount = checked( ( image.Height + 5 ) / 6 );
		if ( band is < 0 || bandCount <= band ) {
			throw new ArgumentOutOfRangeException( nameof( band ) );
		}
		int expectedMaskLength = checked( image.Palette.Length * image.Width );
		if ( expectedMaskLength != bandMasks.Length ) {
			throw new ArgumentException(
				"The Sixel band-mask workspace does not match the image palette and width.",
				nameof( bandMasks )
			);
		}
		if ( image.Palette.Length != bandUsedRegisters.Length ) {
			throw new ArgumentException(
				"The Sixel band-register workspace does not match the image palette.",
				nameof( bandUsedRegisters )
			);
		}

		ReadOnlySpan<byte> indices = image.Indices.Span;
		ReadOnlySpan<byte> transparency = image.TransparentMask.Span;
		int firstRow = checked( band * 6 );
		int exclusiveLastRow = Math.Min(
			image.Height,
			checked( firstRow + 6 )
		);
		for ( int row = firstRow; row < exclusiveLastRow; row++ ) {
			int rowWithinBand = row - firstRow;
			byte bit = checked( (byte)( 1 << rowWithinBand ) );
			int rowOffset = checked( row * image.Width );
			for ( int x = 0; x < image.Width; x++ ) {
				int pixelIndex = rowOffset + x;
				if ( 0 != transparency.Length && 0 != transparency[ pixelIndex ] ) {
					continue;
				}

				int register = indices[ pixelIndex ];
				if ( image.Palette.Length <= register ) {
					throw new InvalidOperationException(
						"A Sixel palette image contains an opaque pixel whose palette index is not defined."
					);
				}
				int maskOffset = checked(
					register * image.Width + x
				);
				bandMasks[ maskOffset ] |= bit;
				bandUsedRegisters[ register ] = true;
			}
		}
	}

	private static byte[] EncodeColorPass(
		int register,
		byte[] masks,
		int offset,
		int length
	) {
		ArgumentNullException.ThrowIfNull( masks );
		if ( offset is < 0 || masks.Length < offset ) {
			throw new ArgumentOutOfRangeException( nameof( offset ) );
		}
		if ( length is < 1 || masks.Length - offset < length ) {
			throw new ArgumentOutOfRangeException( nameof( length ) );
		}

		return EncodeColorPassCore(
			register,
			masks.AsSpan(
				offset,
				length
			)
		);
	}

	private static byte[] EncodeColorPassCore(
		int register,
		ReadOnlySpan<byte> masks
	) {
		if ( register is < 0 or > SixelCodec.MaximumColorRegister ) {
			throw new ArgumentOutOfRangeException( nameof( register ) );
		}
		if ( masks.IsEmpty ) {
			throw new ArgumentException(
				"A Sixel color pass requires at least one mask column.",
				nameof( masks )
			);
		}

		int lastNonZero = -1;
		for ( int index = masks.Length - 1; index >= 0; index-- ) {
			if ( 0 != masks[ index ] ) {
				lastNonZero = index;
				break;
			}
		}
		if ( 0 > lastNonZero ) {
			throw new ArgumentException(
				"A Sixel color pass requires at least one nonzero mask column.",
				nameof( masks )
			);
		}

		ReadOnlySpan<byte> encodedMasks = masks.Slice(
			0,
			lastNonZero + 1
		);
		byte[] selection = SixelCodec.EncodeColorSelection( register );
		int dataLength = GetEncodedRunDataLength( encodedMasks );
		byte[] result = new byte[ checked( selection.Length + dataLength ) ];
		selection.CopyTo(
			result,
			0
		);
		WriteEncodedRunData(
			encodedMasks,
			result.AsSpan( selection.Length )
		);
		return result;
	}

	private static int GetEncodedRunDataLength(
		ReadOnlySpan<byte> masks
	) {
		if ( masks.IsEmpty ) {
			throw new ArgumentException(
				"Sixel run-data length calculation requires at least one mask column.",
				nameof( masks )
			);
		}

		int encodedLength = 0;
		int runStart = 0;
		while ( runStart < masks.Length ) {
			byte value = masks[ runStart ];
			ValidateMaskValue( value );
			int runEnd = runStart + 1;
			while ( runEnd < masks.Length && masks[ runEnd ] == value ) {
				runEnd++;
			}

			int runLength = runEnd - runStart;
			encodedLength = checked(
				encodedLength + GetCanonicalRunLength( runLength )
			);
			runStart = runEnd;
		}

		return encodedLength;
	}

	private static void WriteEncodedRunData(
		ReadOnlySpan<byte> masks,
		Span<byte> destination
	) {
		if ( masks.IsEmpty ) {
			throw new ArgumentException(
				"Sixel run-data encoding requires at least one mask column.",
				nameof( masks )
			);
		}

		int requiredLength = GetEncodedRunDataLength( masks );
		if ( destination.Length != requiredLength ) {
			throw new ArgumentException(
				"The Sixel run-data destination length does not match the canonical encoded length.",
				nameof( destination )
			);
		}

		int destinationOffset = 0;
		int runStart = 0;
		while ( runStart < masks.Length ) {
			byte value = masks[ runStart ];
			ValidateMaskValue( value );
			int runEnd = runStart + 1;
			while ( runEnd < masks.Length && masks[ runEnd ] == value ) {
				runEnd++;
			}

			int runLength = runEnd - runStart;
			byte dataCharacter = SixelCodec.EncodeDataCharacter( value );
			if ( ShouldUseRepeat( runLength ) ) {
				destination[ destinationOffset++ ] = (byte)'!';
				destinationOffset += WritePositiveIntegerAscii(
					runLength,
					destination.Slice( destinationOffset )
				);
				destination[ destinationOffset++ ] = dataCharacter;
			} else {
				for ( int index = 0; index < runLength; index++ ) {
					destination[ destinationOffset++ ] = dataCharacter;
				}
			}
			runStart = runEnd;
		}

		if ( destinationOffset != destination.Length ) {
			throw new InvalidOperationException(
				"The Sixel run-data encoder did not fill the expected destination length."
			);
		}
	}

	private static int GetCanonicalRunLength(
		int runLength
	) {
		if ( runLength is < 1 or > SixelCodec.MaximumRepeatCount ) {
			throw new ArgumentOutOfRangeException( nameof( runLength ) );
		}

		return ShouldUseRepeat( runLength )
			? checked( 2 + CountDecimalDigits( runLength ) )
			: runLength
		;
	}

	private static bool ShouldUseRepeat(
		int runLength
	) {
		if ( runLength is < 1 or > SixelCodec.MaximumRepeatCount ) {
			throw new ArgumentOutOfRangeException( nameof( runLength ) );
		}

		int repeatLength = checked( 2 + CountDecimalDigits( runLength ) );
		return repeatLength < runLength;
	}

	private static int CountDecimalDigits(
		int value
	) {
		if ( 0 >= value ) {
			throw new ArgumentOutOfRangeException( nameof( value ) );
		}

		int digits = 1;
		while ( 10 <= value ) {
			value /= 10;
			digits++;
		}

		return digits;
	}

	private static int WritePositiveIntegerAscii(
		int value,
		Span<byte> destination
	) {
		if ( 0 >= value ) {
			throw new ArgumentOutOfRangeException( nameof( value ) );
		}

		int digits = CountDecimalDigits( value );
		if ( destination.Length < digits ) {
			throw new ArgumentException(
				"The destination is too small for the encoded integer.",
				nameof( destination )
			);
		}

		int remaining = value;
		for ( int index = digits - 1; index >= 0; index-- ) {
			destination[ index ] = checked(
				(byte)( (byte)'0' + remaining % 10 )
			);
			remaining /= 10;
		}

		return digits;
	}

	private static void ValidateMaskValue(
		byte value
	) {
		if ( SixelCodec.MaximumSixelDataValue < value ) {
			throw new ArgumentOutOfRangeException(
				nameof( value ),
				value,
				$"A Sixel band mask must be between 0 and {SixelCodec.MaximumSixelDataValue}."
			);
		}
	}
}
