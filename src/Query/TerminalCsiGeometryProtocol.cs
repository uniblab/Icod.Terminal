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
/// Implements bounded CSI terminal-window and character-cell pixel-size queries.
/// </summary>
internal static class TerminalCsiGeometryProtocol {
	internal const int MaximumPixelDimension = 1_000_000;

	private const int TerminalPixelResponseSelector = 4;
	private const int CellPixelResponseSelector = 6;

	internal static ReadOnlyMemory<byte> TerminalPixelSizeRequest {
		get;
	} = CsiWriter.EncodeFrame(
		[ (byte)'1', (byte)'4' ],
		ReadOnlySpan<byte>.Empty,
		(byte)'t'
	);

	internal static ReadOnlyMemory<byte> CellPixelSizeRequest {
		get;
	} = CsiWriter.EncodeFrame(
		[ (byte)'1', (byte)'6' ],
		ReadOnlySpan<byte>.Empty,
		(byte)'t'
	);

	internal static ITerminalResponseMatcher TerminalPixelSizeMatcher {
		get;
	} = new GeometryResponseMatcher( TerminalPixelResponseSelector );

	internal static ITerminalResponseMatcher CellPixelSizeMatcher {
		get;
	} = new GeometryResponseMatcher( CellPixelResponseSelector );

	internal static TerminalPixelSize ParseTerminalPixelSize(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		return ParsePixelSize(
			frame,
			TerminalPixelResponseSelector,
			"terminal-window"
		);
	}

	internal static TerminalPixelSize ParseCellPixelSize(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		return ParsePixelSize(
			frame,
			CellPixelResponseSelector,
			"character-cell"
		);
	}

	private static TerminalPixelSize ParsePixelSize(
		TerminalResponseFrame frame,
		int expectedSelector,
		string description
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( 0 > expectedSelector ) {
			throw new ArgumentOutOfRangeException( nameof( expectedSelector ) );
		}
		ArgumentException.ThrowIfNullOrWhiteSpace( description );

		TerminalCsiSyntax syntax = TerminalCsiSyntax.Parse( frame );
		TerminalCsiParameterSemantics.RequireFinalByte(
			syntax,
			(byte)'t'
		);
		TerminalCsiParameterSemantics.RequireNoPrivateParameterBytes( syntax );
		TerminalCsiParameterSemantics.RequireNoIntermediateBytes( syntax );

		int[] parameters = TerminalCsiParameterSemantics.GetRequiredNumericParameters(
			syntax,
			maximumCount: 3,
			maximumValue: MaximumPixelDimension
		);
		if ( 3 != parameters.Length || expectedSelector != parameters[ 0 ] ) {
			throw new FormatException(
				$"A CSI {description} pixel-size response must contain selector {expectedSelector}, height, and width."
			);
		}
		if ( 0 >= parameters[ 1 ] || 0 >= parameters[ 2 ] ) {
			throw new FormatException(
				$"A CSI {description} pixel-size response must contain positive height and width values."
			);
		}

		return new TerminalPixelSize(
			parameters[ 2 ],
			parameters[ 1 ]
		);
	}

	private sealed class GeometryResponseMatcher : ITerminalResponseMatcher {
		private readonly int responseSelector;

		internal GeometryResponseMatcher(
			int responseSelector
		) {
			if ( 0 > responseSelector ) {
				throw new ArgumentOutOfRangeException( nameof( responseSelector ) );
			}

			this.responseSelector = responseSelector;
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
				|| (byte)'t' != structure.FinalByte.Value
				|| !structure.IntermediateBytes.IsEmpty ) {
				return false;
			}

			try {
				TerminalCsiSyntax syntax = TerminalCsiSyntax.Parse( structure );
				if ( !syntax.PrivateParameterBytes.IsEmpty ) {
					return false;
				}

				TerminalCsiNumericComponent selector =
					TerminalCsiParameterSemantics.GetNumericParameter(
						syntax,
						0,
						MaximumPixelDimension
					);
				return !selector.HasValue
					|| this.responseSelector == selector.Value;
			} catch ( FormatException ) {
				return true;
			}
		}
	}
}
