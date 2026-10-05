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

using System.Text;

/// <summary>
/// Unsolicited semantic-event routing for <see cref="TerminalInputDecoder"/>.
/// </summary>
internal sealed partial class TerminalInputDecoder {
	private enum EnvironmentSemanticFrameKind {
		Appearance,
		InBandResize,
		AppearanceMode,
		InBandResizeMode
	}

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

	private static readonly byte[] AppearanceSevenBitPrefix =
		Encoding.ASCII.GetBytes( "\u001b[?997;" );

	private static readonly byte[] AppearanceEightBitPrefix = [
		0x9B,
		.. Encoding.ASCII.GetBytes( "?997;" )
	];

	private static readonly byte[] InBandResizeSevenBitPrefix =
		Encoding.ASCII.GetBytes( "\u001b[48;" );

	private static readonly byte[] InBandResizeEightBitPrefix = [
		0x9B,
		.. Encoding.ASCII.GetBytes( "48;" )
	];

	private static readonly byte[] AppearanceModeSevenBitPrefix =
		Encoding.ASCII.GetBytes( "\u001b[?2031;" );

	private static readonly byte[] AppearanceModeEightBitPrefix = [
		0x9B,
		.. Encoding.ASCII.GetBytes( "?2031;" )
	];

	private static readonly byte[] InBandResizeModeSevenBitPrefix =
		Encoding.ASCII.GetBytes( "\u001b[?2048;" );

	private static readonly byte[] InBandResizeModeEightBitPrefix = [
		0x9B,
		.. Encoding.ASCII.GetBytes( "?2048;" )
	];

	private async ValueTask<TerminalInputDecodeResult?> TryRouteSemanticEventAsync(
		CancellationToken cancellationToken
	) {
		TerminalInputDecodeResult? osc99 =
			await this.TryRouteOsc99SemanticEventAsync(
				cancellationToken
			).ConfigureAwait( false );
		if ( osc99.HasValue ) {
			return osc99.Value;
		}

		return await this.TryRouteEnvironmentSemanticEventAsync(
			cancellationToken
		).ConfigureAwait( false );
	}

	private async ValueTask<TerminalInputDecodeResult?> TryRouteOsc99SemanticEventAsync(
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
					return null;

				case TerminalResponseFrameParseStatus.Invalid:
					await this.DiscardInvalidSemanticReportAsync(
						TerminalControlFamily.Osc,
						maximumFrameBytes,
						cancellationToken
					).ConfigureAwait( false );
					return TerminalInputDecodeResult.RestartRouting();

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
						return TerminalInputDecodeResult.RestartRouting();
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

	private async ValueTask<TerminalInputDecodeResult?> TryRouteEnvironmentSemanticEventAsync(
		CancellationToken cancellationToken
	) {
		while ( true ) {
			if ( !this.TryGetPotentialEnvironmentSemanticPrefix(
				out bool prefixComplete,
				out EnvironmentSemanticFrameKind frameKind
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
				TerminalControlFamily.Csi,
				maximumFrameBytes
			);

			switch ( parseResult.Status ) {
				case TerminalResponseFrameParseStatus.NotCandidate:
					return null;

				case TerminalResponseFrameParseStatus.Invalid:
					await this.DiscardInvalidSemanticReportAsync(
						TerminalControlFamily.Csi,
						maximumFrameBytes,
						cancellationToken
					).ConfigureAwait( false );
					return TerminalInputDecodeResult.RestartRouting();

				case TerminalResponseFrameParseStatus.Incomplete:
					if ( !await this.ReadMoreAsync(
						cancellationToken
					).ConfigureAwait( false ) ) {
						return null;
					}
					continue;

				case TerminalResponseFrameParseStatus.Complete:
					TerminalResponseFrame frame = this.CreateResponseFrame(
						TerminalControlFamily.Csi,
						parseResult.Length
					);
					TerminalSemanticEvent? semanticEvent = null;
					try {
						switch ( frameKind ) {
							case EnvironmentSemanticFrameKind.Appearance:
								semanticEvent = TerminalSemanticEvent.FromAppearance(
									new TerminalAppearanceEvent(
										TerminalEnvironmentProtocol.ParseAppearance( frame )
									)
								);
								break;

							case EnvironmentSemanticFrameKind.InBandResize:
								semanticEvent = TerminalSemanticEvent.FromInBandResize(
									TerminalEnvironmentProtocol.ParseInBandResize( frame )
								);
								break;

							case EnvironmentSemanticFrameKind.AppearanceMode:
								_ = TerminalEnvironmentProtocol.ParsePrivateModeState(
									frame,
									2031
								);
								break;

							case EnvironmentSemanticFrameKind.InBandResizeMode:
								_ = TerminalEnvironmentProtocol.ParsePrivateModeState(
									frame,
									2048
								);
								break;

							default:
								throw new InvalidOperationException(
									$"Unexpected environment semantic frame kind '{frameKind}'."
								);
						}
					} catch ( FormatException ) {
						this.Consume( parseResult.Length );
						return TerminalInputDecodeResult.RestartRouting();
					}

					this.Consume( parseResult.Length );
					return semanticEvent is null
						? TerminalInputDecodeResult.RestartRouting()
						: TerminalInputDecodeResult.FromSemantic( semanticEvent )
					;

				default:
					throw new InvalidOperationException(
						$"Unexpected terminal semantic framing status '{parseResult.Status}'."
					);
			}
		}
	}

	private async ValueTask DiscardInvalidSemanticReportAsync(
		TerminalControlFamily family,
		int maximumFrameBytes,
		CancellationToken cancellationToken
	) {
		if ( family is not TerminalControlFamily.Csi
			and not TerminalControlFamily.Osc ) {
			throw new ArgumentOutOfRangeException( nameof( family ) );
		}
		if ( 4 > maximumFrameBytes
			|| TerminalResponseFramer.DefaultMaximumFrameBytes < maximumFrameBytes ) {
			throw new ArgumentOutOfRangeException( nameof( maximumFrameBytes ) );
		}

		TerminalControlSequenceScanner scanner = new(
			TerminalResponseFramer.HardMaximumFrameBytes
		);
		foreach ( byte value in this.bufferedBytes ) {
			TerminalResponseFrameParseStatus status = scanner.Feed( value );
			if ( TerminalResponseFrameParseStatus.Invalid == status ) {
				this.Consume( scanner.Length );
				return;
			}
			if ( TerminalResponseFrameParseStatus.Complete == status
				|| TerminalResponseFrameParseStatus.NotCandidate == status ) {
				throw new InvalidOperationException(
					"Invalid semantic framing did not reproduce while locating its discard boundary."
				);
			}
		}

		if ( this.bufferedBytes.Count < maximumFrameBytes ) {
			throw new InvalidOperationException(
				"Invalid semantic framing did not identify a structural or size boundary."
			);
		}

		this.Consume( this.bufferedBytes.Count );
		await this.DrainOversizedResponseAsync(
			family,
			cancellationToken
		).ConfigureAwait( false );
	}

	private bool TryGetPotentialEnvironmentSemanticPrefix(
		out bool prefixComplete,
		out EnvironmentSemanticFrameKind frameKind
	) {
		prefixComplete = false;
		frameKind = default;
		if ( 0 == this.bufferedBytes.Count ) {
			return false;
		}

		(ReadOnlyMemory<byte> Prefix, EnvironmentSemanticFrameKind Kind)[] candidates =
			this.bufferedBytes[ 0 ] switch {
				EscapeByte => [
					( AppearanceSevenBitPrefix, EnvironmentSemanticFrameKind.Appearance ),
					( InBandResizeSevenBitPrefix, EnvironmentSemanticFrameKind.InBandResize ),
					( AppearanceModeSevenBitPrefix, EnvironmentSemanticFrameKind.AppearanceMode ),
					( InBandResizeModeSevenBitPrefix, EnvironmentSemanticFrameKind.InBandResizeMode )
				],
				0x9B => [
					( AppearanceEightBitPrefix, EnvironmentSemanticFrameKind.Appearance ),
					( InBandResizeEightBitPrefix, EnvironmentSemanticFrameKind.InBandResize ),
					( AppearanceModeEightBitPrefix, EnvironmentSemanticFrameKind.AppearanceMode ),
					( InBandResizeModeEightBitPrefix, EnvironmentSemanticFrameKind.InBandResizeMode )
				],
				_ => []
			};

		foreach ( (ReadOnlyMemory<byte> prefix, EnvironmentSemanticFrameKind kind) in candidates ) {
			int compareCount = Math.Min(
				this.bufferedBytes.Count,
				prefix.Length
			);
			bool matches = true;
			for ( int index = 0; index < compareCount; ++index ) {
				if ( this.bufferedBytes[ index ] != prefix.Span[ index ] ) {
					matches = false;
					break;
				}
			}
			if ( !matches ) {
				continue;
			}

			prefixComplete = prefix.Length <= this.bufferedBytes.Count;
			frameKind = kind;
			return true;
		}

		return false;
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
