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
/// Encodes and strictly parses Terminal 1.27 appearance and in-band resize controls.
/// </summary>
internal static class TerminalEnvironmentProtocol {
	private const int AppearanceQuerySelector = 996;
	private const int AppearanceReportSelector = 997;
	private const int InBandResizeReportSelector = 48;

	internal static ReadOnlyMemory<byte> AppearanceQueryRequest {
		get;
	} = CsiWriter.EncodeFrame(
		Encoding.ASCII.GetBytes( $"?{AppearanceQuerySelector}" ),
		ReadOnlySpan<byte>.Empty,
		(byte)'n'
	);

	internal static ITerminalResponseMatcher AppearanceReportMatcher {
		get;
	} = new EnvironmentResponseMatcher(
		AppearanceReportSelector,
		privateParametersRequired: true,
		ReadOnlyMemory<byte>.Empty,
		(byte)'n'
	);

	internal static ReadOnlyMemory<byte> CreatePrivateModeQuery(
		int mode
	) {
		ValidateMode( mode );
		return CsiWriter.EncodeFrame(
			Encoding.ASCII.GetBytes(
				"?" + mode.ToString( CultureInfo.InvariantCulture )
			),
			[ (byte)'$' ],
			(byte)'p'
		);
	}

	internal static ReadOnlyMemory<byte> CreatePrivateModeSet(
		int mode,
		bool enabled
	) {
		ValidateMode( mode );
		return CsiWriter.EncodeDecPrivateModeFrame( mode, enabled );
	}

	internal static ITerminalResponseMatcher CreatePrivateModeReportMatcher(
		int mode
	) {
		ValidateMode( mode );
		return new EnvironmentResponseMatcher(
			mode,
			privateParametersRequired: true,
			new byte[] { (byte)'$' },
			(byte)'y'
		);
	}

	internal static TerminalAppearance ParseAppearance(
		TerminalResponseFrame frame
	) {
		TerminalCsiSyntax syntax = ParseSyntax(
			frame,
			(byte)'n',
			privateParametersRequired: true,
			ReadOnlySpan<byte>.Empty
		);
		int[] parameters = ParseParametersWithoutSubparameters(
			syntax,
			expectedCount: 2
		);
		if ( AppearanceReportSelector != parameters[ 0 ] ) {
			throw new FormatException(
				$"An appearance report must use selector {AppearanceReportSelector}."
			);
		}

		return parameters[ 1 ] switch {
			(int)TerminalAppearance.Dark => TerminalAppearance.Dark,
			(int)TerminalAppearance.Light => TerminalAppearance.Light,
			_ => throw new FormatException(
				"An appearance report must identify a dark or light appearance."
			)
		};
	}

	internal static TerminalPrivateModeState ParsePrivateModeState(
		TerminalResponseFrame frame,
		int expectedMode
	) {
		ValidateMode( expectedMode );
		TerminalCsiSyntax syntax = ParseSyntax(
			frame,
			(byte)'y',
			privateParametersRequired: true,
			[ (byte)'$' ]
		);
		int[] parameters = ParseParametersWithoutSubparameters(
			syntax,
			expectedCount: 2
		);
		if ( expectedMode != parameters[ 0 ] ) {
			throw new FormatException(
				$"The private-mode report does not describe requested mode {expectedMode}."
			);
		}
		if ( parameters[ 1 ] > (int)TerminalPrivateModeState.PermanentlyReset ) {
			throw new FormatException(
				"A private-mode report contains an unrecognized state."
			);
		}

		return (TerminalPrivateModeState)parameters[ 1 ];
	}

	internal static TerminalInBandResizeEvent ParseInBandResize(
		TerminalResponseFrame frame
	) {
		TerminalCsiSyntax syntax = ParseSyntax(
			frame,
			(byte)'t',
			privateParametersRequired: false,
			ReadOnlySpan<byte>.Empty
		);
		ReadOnlySpan<TerminalCsiParameter> parameters = syntax.Parameters.Span;
		if ( 5 != parameters.Length ) {
			throw new FormatException(
				"An in-band resize report must contain selector, rows, columns, pixel height, and pixel width."
			);
		}

		int selector = ParseResizeParameter( parameters[ 0 ] );
		int rows = ParseResizeParameter( parameters[ 1 ] );
		int columns = ParseResizeParameter( parameters[ 2 ] );
		int pixelHeight = ParseResizeParameter( parameters[ 3 ] );
		int pixelWidth = ParseResizeParameter( parameters[ 4 ] );
		if ( InBandResizeReportSelector != selector ) {
			throw new FormatException(
				$"An in-band resize report must use selector {InBandResizeReportSelector}."
			);
		}
		if ( 0 >= rows || 0 >= columns ) {
			throw new FormatException(
				"An in-band resize report must contain positive row and column values."
			);
		}
		if ( ( 0 == pixelHeight ) != ( 0 == pixelWidth ) ) {
			throw new FormatException(
				"An in-band resize report must contain either two positive pixel values or two zero values."
			);
		}

		return new TerminalInBandResizeEvent(
			new TerminalDimensions( columns, rows ),
			0 == pixelWidth
				? null
				: new TerminalPixelDimensions( pixelWidth, pixelHeight )
		);
	}

	private static TerminalCsiSyntax ParseSyntax(
		TerminalResponseFrame frame,
		byte finalByte,
		bool privateParametersRequired,
		ReadOnlySpan<byte> intermediateBytes
	) {
		ArgumentNullException.ThrowIfNull( frame );
		TerminalCsiSyntax syntax = TerminalCsiSyntax.Parse( frame );
		TerminalCsiParameterSemantics.RequireFinalByte( syntax, finalByte );
		if ( privateParametersRequired ) {
			TerminalCsiParameterSemantics.RequireExactPrivateParameterBytes(
				syntax,
				[ (byte)'?' ]
			);
		} else {
			TerminalCsiParameterSemantics.RequireNoPrivateParameterBytes( syntax );
		}
		if ( !syntax.IntermediateBytes.Span.SequenceEqual( intermediateBytes ) ) {
			throw new FormatException(
				"The CSI environment report contains unexpected intermediate bytes."
			);
		}
		return syntax;
	}

	private static int[] ParseParametersWithoutSubparameters(
		TerminalCsiSyntax syntax,
		int expectedCount
	) {
		ReadOnlySpan<TerminalCsiParameter> parameters = syntax.Parameters.Span;
		if ( expectedCount != parameters.Length ) {
			throw new FormatException(
				$"The CSI environment report must contain exactly {expectedCount} parameters."
			);
		}

		int[] values = new int[ expectedCount ];
		for ( int index = 0; index < parameters.Length; ++index ) {
			if ( 1 != parameters[ index ].Subparameters.Length ) {
				throw new FormatException(
					"The CSI environment report does not permit subparameters."
				);
			}
			values[ index ] = ParseDecimal( parameters[ index ].RawBytes.Span );
		}
		return values;
	}

	private static int ParseResizeParameter(
		TerminalCsiParameter parameter
	) {
		ReadOnlySpan<TerminalCsiSubparameter> subparameters =
			parameter.Subparameters.Span;
		if ( 0 == subparameters.Length ) {
			throw new FormatException(
				"An in-band resize parameter is missing its primary value."
			);
		}

		int primary = ParseDecimal( subparameters[ 0 ].RawBytes.Span );
		for ( int index = 1; index < subparameters.Length; ++index ) {
			_ = ParseDecimal( subparameters[ index ].RawBytes.Span );
		}
		return primary;
	}

	private static int ParseDecimal(
		ReadOnlySpan<byte> bytes
	) {
		if ( bytes.IsEmpty ) {
			throw new FormatException(
				"A CSI environment numeric field cannot be empty."
			);
		}

		int value = 0;
		for ( int index = 0; index < bytes.Length; ++index ) {
			byte current = bytes[ index ];
			if ( current is < (byte)'0' or > (byte)'9' ) {
				throw new FormatException(
					"A CSI environment numeric field contains a non-decimal byte."
				);
			}

			int digit = current - (byte)'0';
			if ( value > ( int.MaxValue - digit ) / 10 ) {
				throw new FormatException(
					$"A CSI environment numeric field exceeds {int.MaxValue}."
				);
			}
			value = checked( value * 10 + digit );
		}
		return value;
	}

	private static void ValidateMode(
		int mode
	) {
		if ( 0 >= mode ) {
			throw new ArgumentOutOfRangeException( nameof( mode ) );
		}
	}

	private sealed class EnvironmentResponseMatcher :
		ITerminalResponseMatcher,
		ICorrelatedTerminalResponseMatcher {
		private readonly int selector;
		private readonly bool privateParametersRequired;
		private readonly byte[] intermediateBytes;
		private readonly byte finalByte;
		private readonly byte[] sevenBitPrefix;
		private readonly byte[] eightBitPrefix;

		internal EnvironmentResponseMatcher(
			int selector,
			bool privateParametersRequired,
			ReadOnlyMemory<byte> intermediateBytes,
			byte finalByte
		) {
			if ( 0 > selector ) {
				throw new ArgumentOutOfRangeException( nameof( selector ) );
			}
			this.selector = selector;
			this.privateParametersRequired = privateParametersRequired;
			this.intermediateBytes = intermediateBytes.ToArray();
			this.finalByte = finalByte;

			string encodedSelector = selector.ToString(
				CultureInfo.InvariantCulture
			);
			string prefix = privateParametersRequired
				? $"?{encodedSelector};"
				: $"{encodedSelector};"
			;
			byte[] body = Encoding.ASCII.GetBytes( prefix );
			this.sevenBitPrefix = [ 0x1B, (byte)'[', .. body ];
			this.eightBitPrefix = [ 0x9B, .. body ];
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Csi;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			if ( TerminalResponseFrameKind.Csi != frame.Kind
				|| !TerminalControlFrameStructure.TryParse(
					frame,
					out TerminalControlFrameStructure structure
				) ) {
				return false;
			}
			if ( !structure.FinalByte.HasValue
				|| this.finalByte != structure.FinalByte.Value
				|| !structure.IntermediateBytes.Span.SequenceEqual(
					this.intermediateBytes
				) ) {
				return false;
			}

			try {
				TerminalCsiSyntax syntax = TerminalCsiSyntax.Parse( structure );
				bool usesExpectedPrivateParameters =
					syntax.PrivateParameterBytes.Span.SequenceEqual(
						new byte[] { (byte)'?' }
					);
				if ( this.privateParametersRequired
						? !usesExpectedPrivateParameters
						: !syntax.PrivateParameterBytes.IsEmpty ) {
					return false;
				}
				ReadOnlySpan<TerminalCsiParameter> parameters = syntax.Parameters.Span;
				return 2 <= parameters.Length
					&& 1 == parameters[ 0 ].Subparameters.Length
					&& this.selector == ParseDecimal(
						parameters[ 0 ].RawBytes.Span
					)
				;
			} catch ( FormatException ) {
				return IsCorrelatedPrefix( frame.Bytes.ToArray() );
			}
		}

		public bool IsCorrelatedPrefix(
			IReadOnlyList<byte> bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			return StartsWith( bytes, this.sevenBitPrefix )
				|| StartsWith( bytes, this.eightBitPrefix )
			;
		}

		private static bool StartsWith(
			IReadOnlyList<byte> bytes,
			IReadOnlyList<byte> prefix
		) {
			if ( bytes.Count < prefix.Count ) {
				return false;
			}
			for ( int index = 0; index < prefix.Count; ++index ) {
				if ( bytes[ index ] != prefix[ index ] ) {
					return false;
				}
			}
			return true;
		}
	}
}
