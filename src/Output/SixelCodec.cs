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

using System.Globalization;
using System.Text;

/// <summary>
/// Encodes bounded canonical Sixel dialect commands above generic DCS framing.
/// </summary>
internal static class SixelCodec {
	internal const int MaximumDimension = 1_000_000;
	internal const int MaximumRepeatCount = 1_000_000;
	internal const int MaximumColorRegister = 255;
	internal const int MaximumRgbComponent = 100;
	internal const int MaximumSixelDataValue = 63;

	internal const byte GraphicsCarriageReturn = (byte)'$';
	internal const byte GraphicsNewLine = (byte)'-';

	/// <summary>
	/// Encodes the canonical Sixel DCS parameter region: Pa=0, Pb=1, Ph=0.
	/// </summary>
	internal static byte[] EncodeCanonicalDcsParameters() {
		return [
			(byte)'0',
			(byte)';',
			(byte)'1',
			(byte)';',
			(byte)'0'
		];
	}

	/// <summary>
	/// Encodes canonical square-pixel raster attributes for one image extent.
	/// </summary>
	internal static byte[] EncodeRasterAttributes(
		int width,
		int height
	) {
		ValidateDimension(
			width,
			nameof( width )
		);
		ValidateDimension(
			height,
			nameof( height )
		);

		return EncodeAscii(
			FormattableString.Invariant(
				$"\"1;1;{width};{height}"
			)
		);
	}

	/// <summary>
	/// Encodes one six-pixel vertical column value as a Sixel data character.
	/// </summary>
	internal static byte EncodeDataCharacter(
		int value
	) {
		if ( value is < 0 or > MaximumSixelDataValue ) {
			throw new ArgumentOutOfRangeException(
				nameof( value ),
				value,
				$"A Sixel data value must be between 0 and {MaximumSixelDataValue}."
			);
		}

		return checked( (byte)( (byte)'?' + value ) );
	}

	/// <summary>
	/// Encodes one explicit Sixel repeat command.
	/// </summary>
	internal static byte[] EncodeRepeat(
		int count,
		int value
	) {
		if ( count is < 1 or > MaximumRepeatCount ) {
			throw new ArgumentOutOfRangeException(
				nameof( count ),
				count,
				$"A Sixel repeat count must be between 1 and {MaximumRepeatCount}."
			);
		}
		byte dataCharacter = EncodeDataCharacter( value );

		string countText = count.ToString( CultureInfo.InvariantCulture );
		byte[] result = new byte[ checked( countText.Length + 2 ) ];
		result[ 0 ] = (byte)'!';
		Encoding.ASCII.GetBytes(
			countText,
			result.AsSpan( 1 )
		);
		result[ ^1 ] = dataCharacter;
		return result;
	}

	/// <summary>
	/// Encodes selection of one Sixel color register.
	/// </summary>
	internal static byte[] EncodeColorSelection(
		int register
	) {
		ValidateColorRegister( register );
		return EncodeAscii(
			FormattableString.Invariant(
				$"#{register}"
			)
		);
	}

	/// <summary>
	/// Encodes one Sixel RGB color-register definition using protocol percentages.
	/// </summary>
	internal static byte[] EncodeRgbColorDefinition(
		int register,
		int red,
		int green,
		int blue
	) {
		ValidateColorRegister( register );
		ValidateRgbComponent(
			red,
			nameof( red )
		);
		ValidateRgbComponent(
			green,
			nameof( green )
		);
		ValidateRgbComponent(
			blue,
			nameof( blue )
		);

		return EncodeAscii(
			FormattableString.Invariant(
				$"#{register};2;{red};{green};{blue}"
			)
		);
	}

	private static byte[] EncodeAscii(
		string value
	) {
		ArgumentNullException.ThrowIfNull( value );
		return Encoding.ASCII.GetBytes( value );
	}

	private static void ValidateDimension(
		int value,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( parameterName );
		if ( value is < 1 or > MaximumDimension ) {
			throw new ArgumentOutOfRangeException(
				parameterName,
				value,
				$"A Sixel raster dimension must be between 1 and {MaximumDimension}."
			);
		}
	}

	private static void ValidateColorRegister(
		int register
	) {
		if ( register is < 0 or > MaximumColorRegister ) {
			throw new ArgumentOutOfRangeException(
				nameof( register ),
				register,
				$"A Sixel color register must be between 0 and {MaximumColorRegister}."
			);
		}
	}

	private static void ValidateRgbComponent(
		int value,
		string parameterName
	) {
		ArgumentNullException.ThrowIfNull( parameterName );
		if ( value is < 0 or > MaximumRgbComponent ) {
			throw new ArgumentOutOfRangeException(
				parameterName,
				value,
				$"A Sixel RGB component must be between 0 and {MaximumRgbComponent}."
			);
		}
	}
}
