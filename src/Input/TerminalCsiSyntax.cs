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
/// Retains one colon-delimited CSI subparameter without assigning dialect semantics.
/// </summary>
internal readonly struct TerminalCsiSubparameter {
	internal TerminalCsiSubparameter(
		ReadOnlyMemory<byte> rawBytes
	) {
		this.RawBytes = rawBytes;
	}

	internal ReadOnlyMemory<byte> RawBytes {
		get;
	}

	internal bool IsEmpty {
		get {
			return this.RawBytes.IsEmpty;
		}
	}
}

/// <summary>
/// Retains one semicolon-delimited CSI parameter and its colon subparameters.
/// </summary>
internal readonly struct TerminalCsiParameter {
	private readonly TerminalCsiSubparameter[]? subparameters;

	internal TerminalCsiParameter(
		ReadOnlyMemory<byte> rawBytes,
		TerminalCsiSubparameter[] subparameters
	) {
		ArgumentNullException.ThrowIfNull( subparameters );
		this.RawBytes = rawBytes;
		this.subparameters = subparameters;
	}

	internal ReadOnlyMemory<byte> RawBytes {
		get;
	}

	internal bool IsEmpty {
		get {
			return this.RawBytes.IsEmpty;
		}
	}

	internal ReadOnlyMemory<TerminalCsiSubparameter> Subparameters {
		get {
			return this.subparameters ?? Array.Empty<TerminalCsiSubparameter>();
		}
	}
}

/// <summary>
/// Provides the complete bounded CSI syntax view used before dialect-specific interpretation.
/// </summary>
internal sealed class TerminalCsiSyntax {
	internal const int MaximumRawParameterBytes = 1024;
	internal const int MaximumParameterCount = 64;
	internal const int MaximumSubparameterCount = 64;

	private readonly TerminalCsiParameter[] parameters;

	private TerminalCsiSyntax(
		bool usesEightBitIntroducer,
		ReadOnlyMemory<byte> rawParameterBytes,
		ReadOnlyMemory<byte> privateParameterBytes,
		ReadOnlyMemory<byte> parameterDataBytes,
		ReadOnlyMemory<byte> intermediateBytes,
		byte finalByte,
		TerminalCsiParameter[] parameters
	) {
		ArgumentNullException.ThrowIfNull( parameters );
		this.UsesEightBitIntroducer = usesEightBitIntroducer;
		this.RawParameterBytes = rawParameterBytes;
		this.PrivateParameterBytes = privateParameterBytes;
		this.ParameterDataBytes = parameterDataBytes;
		this.IntermediateBytes = intermediateBytes;
		this.FinalByte = finalByte;
		this.parameters = parameters;
	}

	internal bool UsesEightBitIntroducer {
		get;
	}

	internal ReadOnlyMemory<byte> RawParameterBytes {
		get;
	}

	internal ReadOnlyMemory<byte> PrivateParameterBytes {
		get;
	}

	internal ReadOnlyMemory<byte> ParameterDataBytes {
		get;
	}

	internal ReadOnlyMemory<byte> IntermediateBytes {
		get;
	}

	internal byte FinalByte {
		get;
	}

	internal ReadOnlyMemory<TerminalCsiParameter> Parameters {
		get {
			return this.parameters;
		}
	}

	internal static TerminalCsiSyntax Parse(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( TerminalResponseFrameKind.Csi != frame.Kind ) {
			throw new FormatException(
				"The terminal response is not a CSI frame."
			);
		}

		return Parse(
			TerminalControlFrameStructure.Parse( frame )
		);
	}

	internal static TerminalCsiSyntax Parse(
		TerminalControlFrameStructure structure
	) {
		if ( TerminalControlFamily.Csi != structure.Family ) {
			throw new FormatException(
				"The terminal control frame is not CSI."
			);
		}
		if ( !structure.FinalByte.HasValue ) {
			throw new FormatException(
				"A CSI frame must contain a final byte."
			);
		}

		ReadOnlyMemory<byte> rawParameters = structure.ParameterBytes;
		if ( MaximumRawParameterBytes < rawParameters.Length ) {
			throw new FormatException(
				$"A CSI frame cannot contain more than {MaximumRawParameterBytes} parameter bytes."
			);
		}

		int privateLength = GetLeadingPrivateParameterByteCount(
			rawParameters.Span
		);
		ReadOnlyMemory<byte> privateParameters = rawParameters.Slice(
			0,
			privateLength
		);
		ReadOnlyMemory<byte> parameterData = rawParameters.Slice(
			privateLength
		);
		TerminalCsiParameter[] parameters = ParseParameters( parameterData );

		return new TerminalCsiSyntax(
			structure.UsesEightBitIntroducer,
			rawParameters,
			privateParameters,
			parameterData,
			structure.IntermediateBytes,
			structure.FinalByte.Value,
			parameters
		);
	}

	private static int GetLeadingPrivateParameterByteCount(
		ReadOnlySpan<byte> bytes
	) {
		int count = 0;
		while ( count < bytes.Length && IsPrivateParameterByte( bytes[ count ] ) ) {
			++count;
		}
		return count;
	}

	private static TerminalCsiParameter[] ParseParameters(
		ReadOnlyMemory<byte> bytes
	) {
		if ( bytes.IsEmpty ) {
			return Array.Empty<TerminalCsiParameter>();
		}

		List<TerminalCsiParameter> parameters = [];
		int start = 0;
		for ( int index = 0; index <= bytes.Length; ++index ) {
			bool atEnd = index == bytes.Length;
			if ( !atEnd && (byte)';' != bytes.Span[ index ] ) {
				continue;
			}

			if ( MaximumParameterCount <= parameters.Count ) {
				throw new FormatException(
					$"A CSI frame cannot contain more than {MaximumParameterCount} parameters."
				);
			}

			ReadOnlyMemory<byte> parameter = bytes.Slice(
				start,
				index - start
			);
			parameters.Add(
				new TerminalCsiParameter(
					parameter,
					ParseSubparameters( parameter )
				)
			);
			start = index + 1;
		}

		return parameters.ToArray();
	}

	private static TerminalCsiSubparameter[] ParseSubparameters(
		ReadOnlyMemory<byte> bytes
	) {
		List<TerminalCsiSubparameter> subparameters = [];
		int start = 0;
		for ( int index = 0; index <= bytes.Length; ++index ) {
			bool atEnd = index == bytes.Length;
			if ( !atEnd && (byte)':' != bytes.Span[ index ] ) {
				continue;
			}

			if ( MaximumSubparameterCount <= subparameters.Count ) {
				throw new FormatException(
					$"A CSI parameter cannot contain more than {MaximumSubparameterCount} subparameters."
				);
			}

			subparameters.Add(
				new TerminalCsiSubparameter(
					bytes.Slice(
						start,
						index - start
					)
				)
			);
			start = index + 1;
		}

		return subparameters.ToArray();
	}

	private static bool IsPrivateParameterByte(
		byte value
	) {
		return value is >= 0x3C and <= 0x3F;
	}
}
