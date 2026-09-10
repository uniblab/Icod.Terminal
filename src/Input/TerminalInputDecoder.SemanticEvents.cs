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
/// Unsolicited semantic-event routing for <see cref="TerminalInputDecoder"/>.
/// </summary>
internal sealed partial class TerminalInputDecoder {
	private static readonly byte[] Osc99SevenBitPrefix = [
		0x1B,
		(byte)']',
		(byte)'9',
		(byte)'9',
		(byte)';'
	];

	private static readonly byte[] Osc99EightBitPrefix = [
		0x9D,
		(byte)'9',
		(byte)'9',
		(byte)';'
	];

	private async ValueTask<TerminalInputDecodeResult?> TryRouteSemanticEventAsync(
		CancellationToken cancellationToken
	) {
		while ( true ) {
			if ( !this.IsPotentialOsc99SemanticPrefix(
				out bool prefixComplete
			) ) {
				return null;
			}

			if ( !prefixComplete ) {
				bool appended = EscapeByte == this.bufferedBytes[ 0 ]
					? await this.ReadMoreWithinEscapeWindowAsync(
						cancellationToken
					).ConfigureAwait( false )
					: await this.ReadMoreAsync(
						cancellationToken
					).ConfigureAwait( false )
				;
				if ( !appended ) {
					return null;
				}
				continue;
			}

			int maximumFrameBytes = Math.Min(
				TerminalResponseFramer.DefaultMaximumFrameBytes,
				this.maximumBufferedBytes
			);
			TerminalResponseFrameParseResult parseResult = TerminalResponseFramer.Parse(
				this.bufferedBytes,
				TerminalControlFamily.Osc,
				maximumFrameBytes
			);

			switch ( parseResult.Status ) {
				case TerminalResponseFrameParseStatus.NotCandidate:
				case TerminalResponseFrameParseStatus.Invalid:
					return null;

				case TerminalResponseFrameParseStatus.Incomplete:
					if ( !await this.ReadMoreAsync(
						cancellationToken
					).ConfigureAwait( false ) ) {
						return null;
					}
					continue;

				case TerminalResponseFrameParseStatus.Complete:
					TerminalResponseFrame frame = this.CreateResponseFrame(
						TerminalControlFamily.Osc,
						parseResult.Length
					);
					TerminalSemanticEvent? semanticEvent;
					try {
						if ( !TerminalOsc99UnsolicitedReportParser.TryParse(
							frame,
							out semanticEvent
						) ) {
							return null;
						}
					} catch ( FormatException ) {
						this.Consume( parseResult.Length );
						throw;
					}

					if ( semanticEvent is null ) {
						throw new InvalidOperationException(
							"A recognized terminal semantic report did not produce an event."
						);
					}
					this.Consume( parseResult.Length );
					return TerminalInputDecodeResult.FromSemantic( semanticEvent );

				default:
					throw new InvalidOperationException(
						$"Unexpected terminal semantic framing status '{parseResult.Status}'."
					);
			}
		}
	}

	private bool IsPotentialOsc99SemanticPrefix(
		out bool prefixComplete
	) {
		prefixComplete = false;
		if ( 0 == this.bufferedBytes.Count ) {
			return false;
		}

		IReadOnlyList<byte>? prefix = this.bufferedBytes[ 0 ] switch {
			EscapeByte => Osc99SevenBitPrefix,
			0x9D => Osc99EightBitPrefix,
			_ => null
		};
		if ( prefix is null ) {
			return false;
		}

		int compareCount = Math.Min(
			this.bufferedBytes.Count,
			prefix.Count
		);
		for ( int index = 0; index < compareCount; ++index ) {
			if ( this.bufferedBytes[ index ] != prefix[ index ] ) {
				return false;
			}
		}

		prefixComplete = prefix.Count <= this.bufferedBytes.Count;
		return true;
	}
}
