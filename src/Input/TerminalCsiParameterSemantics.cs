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
/// Identifies the semantic state of one CSI numeric parameter component.
/// </summary>
internal enum TerminalCsiNumericComponentKind {
	Omitted,
	Empty,
	Numeric
}

/// <summary>
/// Retains one bounded CSI numeric parameter value without collapsing omitted and empty state.
/// </summary>
internal readonly struct TerminalCsiNumericComponent {
	private TerminalCsiNumericComponent(
		TerminalCsiNumericComponentKind kind,
		int value
	) {
		this.Kind = kind;
		this.Value = value;
	}

	internal TerminalCsiNumericComponentKind Kind {
		get;
	}

	internal int Value {
		get;
	}

	internal bool HasValue {
		get {
			return TerminalCsiNumericComponentKind.Numeric == this.Kind;
		}
	}

	internal static TerminalCsiNumericComponent Omitted {
		get {
			return new TerminalCsiNumericComponent(
				TerminalCsiNumericComponentKind.Omitted,
				0
			);
		}
	}

	internal static TerminalCsiNumericComponent Empty {
		get {
			return new TerminalCsiNumericComponent(
				TerminalCsiNumericComponentKind.Empty,
				0
			);
		}
	}

	internal static TerminalCsiNumericComponent Numeric(
		int value
	) {
		if ( 0 > value ) {
			throw new ArgumentOutOfRangeException( nameof( value ) );
		}

		return new TerminalCsiNumericComponent(
			TerminalCsiNumericComponentKind.Numeric,
			value
		);
	}
}

/// <summary>
/// Provides bounded semantic conversion and dialect-policy helpers over parsed CSI syntax.
/// </summary>
internal static class TerminalCsiParameterSemantics {
	internal const int DefaultMaximumNumericValue = 1_000_000;

	internal static TerminalCsiNumericComponent GetNumericParameter(
		TerminalCsiSyntax syntax,
		int index,
		int maximumValue = DefaultMaximumNumericValue
	) {
		ArgumentNullException.ThrowIfNull( syntax );
		if ( 0 > index ) {
			throw new ArgumentOutOfRangeException( nameof( index ) );
		}
		if ( 0 > maximumValue ) {
			throw new ArgumentOutOfRangeException( nameof( maximumValue ) );
		}

		ReadOnlySpan<TerminalCsiParameter> parameters = syntax.Parameters.Span;
		if ( parameters.Length <= index ) {
			return TerminalCsiNumericComponent.Omitted;
		}

		TerminalCsiParameter parameter = parameters[ index ];
		RequireNoSubparameters( parameter );
		return ParseNumericComponent(
			parameter.RawBytes.Span,
			maximumValue
		);
	}

	internal static int[] GetRequiredNumericParameters(
		TerminalCsiSyntax syntax,
		int maximumCount,
		int maximumValue = DefaultMaximumNumericValue
	) {
		ArgumentNullException.ThrowIfNull( syntax );
		if ( 0 > maximumCount ) {
			throw new ArgumentOutOfRangeException( nameof( maximumCount ) );
		}
		if ( 0 > maximumValue ) {
			throw new ArgumentOutOfRangeException( nameof( maximumValue ) );
		}

		ReadOnlySpan<TerminalCsiParameter> parameters = syntax.Parameters.Span;
		if ( maximumCount < parameters.Length ) {
			throw new FormatException(
				$"A CSI response cannot contain more than {maximumCount} numeric parameters."
			);
		}

		int[] values = new int[ parameters.Length ];
		for ( int index = 0; index < parameters.Length; ++index ) {
			TerminalCsiParameter parameter = parameters[ index ];
			RequireNoSubparameters( parameter );
			TerminalCsiNumericComponent component = ParseNumericComponent(
				parameter.RawBytes.Span,
				maximumValue
			);
			if ( !component.HasValue ) {
				throw new FormatException(
					"A CSI response contains an empty numeric parameter."
				);
			}
			values[ index ] = component.Value;
		}

		return values;
	}

	internal static void RequireNoPrivateParameterBytes(
		TerminalCsiSyntax syntax
	) {
		ArgumentNullException.ThrowIfNull( syntax );
		if ( !syntax.PrivateParameterBytes.IsEmpty ) {
			throw new FormatException(
				"The CSI response unexpectedly uses private parameter bytes."
			);
		}
	}

	internal static void RequireExactPrivateParameterBytes(
		TerminalCsiSyntax syntax,
		ReadOnlySpan<byte> expected
	) {
		ArgumentNullException.ThrowIfNull( syntax );
		if ( !syntax.PrivateParameterBytes.Span.SequenceEqual( expected ) ) {
			throw new FormatException(
				"The CSI response has unexpected private parameter bytes."
			);
		}
	}

	internal static void RequireNoIntermediateBytes(
		TerminalCsiSyntax syntax
	) {
		ArgumentNullException.ThrowIfNull( syntax );
		if ( !syntax.IntermediateBytes.IsEmpty ) {
			throw new FormatException(
				"The CSI response contains unexpected intermediate bytes."
			);
		}
	}

	internal static void RequireFinalByte(
		TerminalCsiSyntax syntax,
		byte expected
	) {
		ArgumentNullException.ThrowIfNull( syntax );
		if ( expected is < 0x40 or > 0x7E ) {
			throw new ArgumentOutOfRangeException( nameof( expected ) );
		}
		if ( expected != syntax.FinalByte ) {
			throw new FormatException(
				"The CSI response has an unexpected final byte."
			);
		}
	}

	private static TerminalCsiNumericComponent ParseNumericComponent(
		ReadOnlySpan<byte> bytes,
		int maximumValue
	) {
		if ( bytes.IsEmpty ) {
			return TerminalCsiNumericComponent.Empty;
		}

		int value = 0;
		for ( int index = 0; index < bytes.Length; ++index ) {
			byte current = bytes[ index ];
			if ( current is < (byte)'0' or > (byte)'9' ) {
				throw new FormatException(
					"A CSI numeric parameter contains a non-decimal byte."
				);
			}

			int digit = current - (byte)'0';
			if ( value > ( maximumValue - digit ) / 10 ) {
				throw new FormatException(
					$"A CSI numeric parameter exceeds the supported maximum of {maximumValue}."
				);
			}
			value = checked( value * 10 + digit );
		}

		return TerminalCsiNumericComponent.Numeric( value );
	}

	private static void RequireNoSubparameters(
		TerminalCsiParameter parameter
	) {
		if ( 1 != parameter.Subparameters.Length ) {
			throw new FormatException(
				"A CSI numeric parameter unexpectedly contains colon subparameters."
			);
		}
	}
}
