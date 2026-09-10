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

internal sealed partial class TerminalInputDecoder {
	private readonly object kittyGraphicsProbeGate = new();
	private KittyGraphicsSupportProbe? kittyGraphicsSupportProbe;

	internal KittyGraphicsSupportProbe RegisterKittyGraphicsSupportProbe(
		uint imageId
	) {
		if ( 0 == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A Kitty Graphics support-query image id must be non-zero."
			);
		}

		lock ( this.kittyGraphicsProbeGate ) {
			if ( this.kittyGraphicsSupportProbe is not null ) {
				throw new InvalidOperationException(
					"The terminal input decoder already has an active Kitty Graphics support probe."
				);
			}

			KittyGraphicsSupportProbe probe = new( imageId );
			this.kittyGraphicsSupportProbe = probe;
			return probe;
		}
	}

	internal void RemoveKittyGraphicsSupportProbe(
		KittyGraphicsSupportProbe probe
	) {
		ArgumentNullException.ThrowIfNull( probe );
		lock ( this.kittyGraphicsProbeGate ) {
			if ( ReferenceEquals( this.kittyGraphicsSupportProbe, probe ) ) {
				this.kittyGraphicsSupportProbe = null;
			}
		}
	}

	private async ValueTask<bool> TryConsumeKittyGraphicsSupportProbeAsync(
		CancellationToken cancellationToken
	) {
		KittyGraphicsSupportProbe? probe;
		lock ( this.kittyGraphicsProbeGate ) {
			probe = this.kittyGraphicsSupportProbe;
		}
		if ( probe is null || 0 == this.bufferedBytes.Count ) {
			return false;
		}

		while ( true ) {
			if ( !TryGetKittyGraphicsApcPayloadStart(
				this.bufferedBytes,
				out int payloadStart,
				out bool introducerComplete
			) ) {
				return false;
			}
			if ( !introducerComplete || this.bufferedBytes.Count <= payloadStart ) {
				if ( this.endOfInput
					|| !await this.ReadMoreAsync(
						cancellationToken
					).ConfigureAwait( false ) ) {
					return false;
				}
				continue;
			}
			if ( (byte)'G' != this.bufferedBytes[ payloadStart ] ) {
				return false;
			}

			bool correlatedPrefix = KittyGraphicsCapabilityProtocol.IsCorrelatedResponsePrefix(
				this.bufferedBytes,
				probe.ImageId
			);
			if ( correlatedPrefix ) {
				probe.RecordCorrelation();
			}

			TerminalResponseFrameParseResult parseResult = TerminalResponseFramer.Parse(
				this.bufferedBytes,
				TerminalControlFamily.Apc,
				TerminalResponseFramer.DefaultMaximumFrameBytes
			);
			switch ( parseResult.Status ) {
				case TerminalResponseFrameParseStatus.NotCandidate:
					return false;

				case TerminalResponseFrameParseStatus.Invalid:
					if ( !correlatedPrefix ) {
						return false;
					}

					int invalidLength = FindInvalidKittyGraphicsApcLength(
						this.bufferedBytes
					);
					if ( TerminalResponseFramer.DefaultMaximumFrameBytes <= invalidLength ) {
						probe.RecordFailure(
							new FormatException(
								$"The correlated Kitty Graphics response exceeded the {TerminalResponseFramer.DefaultMaximumFrameBytes}-byte framing limit."
							)
						);
						await this.DrainOversizedResponseAsync(
							TerminalControlFamily.Apc,
							cancellationToken
						).ConfigureAwait( false );
					} else {
						this.Consume( invalidLength );
						probe.RecordFailure(
							new FormatException(
								"The correlated Kitty Graphics response was aborted or structurally malformed."
							)
						);
					}

					if ( 0 == this.bufferedBytes.Count && !this.endOfInput ) {
						_ = await this.ReadMoreAsync(
							cancellationToken
						).ConfigureAwait( false );
					}
					return true;

				case TerminalResponseFrameParseStatus.Incomplete:
					if ( this.endOfInput
						|| !await this.ReadMoreAsync(
							cancellationToken
						).ConfigureAwait( false ) ) {
						return false;
					}
					continue;

				case TerminalResponseFrameParseStatus.Complete:
					TerminalResponseFrame frame = this.CreateResponseFrame(
						TerminalControlFamily.Apc,
						parseResult.Length
					);
					if ( !KittyGraphicsCapabilityProtocol.IsCorrelatedResponse(
						frame,
						probe.ImageId
					) ) {
						return false;
					}

					probe.RecordCorrelation();
					this.Consume( parseResult.Length );
					try {
						KittyGraphicsResponse response = KittyGraphicsCodec.ParseResponse( frame );
						if ( probe.ImageId != response.ImageId ) {
							throw new FormatException(
								"The Kitty Graphics response does not match the active support-query image id."
							);
						}
						probe.RecordResponse( response );
					} catch ( FormatException exception ) {
						probe.RecordFailure( exception );
					}

					if ( 0 == this.bufferedBytes.Count && !this.endOfInput ) {
						_ = await this.ReadMoreAsync(
							cancellationToken
						).ConfigureAwait( false );
					}
					return true;

				default:
					throw new InvalidOperationException(
						$"Unexpected terminal response framing status '{parseResult.Status}'."
					);
			}
		}
	}

	private static int FindInvalidKittyGraphicsApcLength(
		IReadOnlyList<byte> bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		TerminalControlSequenceScanner scanner = new(
			TerminalResponseFramer.DefaultMaximumFrameBytes
		);
		for ( int index = 0; index < bytes.Count; index++ ) {
			TerminalResponseFrameParseStatus status = scanner.Feed( bytes[ index ] );
			if ( TerminalResponseFrameParseStatus.Invalid == status ) {
				return index + 1;
			}
			if ( status is TerminalResponseFrameParseStatus.Complete
				or TerminalResponseFrameParseStatus.NotCandidate ) {
				break;
			}
		}

		throw new InvalidOperationException(
			"The Kitty Graphics APC was reported invalid without an identifiable invalid boundary."
		);
	}

	private static bool TryGetKittyGraphicsApcPayloadStart(
		IReadOnlyList<byte> bytes,
		out int payloadStart,
		out bool introducerComplete
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		payloadStart = -1;
		introducerComplete = false;
		if ( 0 == bytes.Count ) {
			return false;
		}

		if ( 0x9F == bytes[ 0 ] ) {
			payloadStart = 1;
			introducerComplete = true;
			return true;
		}
		if ( EscapeByte != bytes[ 0 ] ) {
			return false;
		}
		if ( 1 == bytes.Count ) {
			payloadStart = 2;
			return true;
		}
		if ( (byte)'_' != bytes[ 1 ] ) {
			return false;
		}

		payloadStart = 2;
		introducerComplete = true;
		return true;
	}
}

internal sealed class KittyGraphicsSupportProbe {
	private readonly object sync = new();
	private KittyGraphicsResponse? response;
	private FormatException? failure;
	private int correlationObserved;

	internal KittyGraphicsSupportProbe(
		uint imageId
	) {
		if ( 0 == imageId ) {
			throw new ArgumentOutOfRangeException(
				nameof( imageId ),
				imageId,
				"A Kitty Graphics support-query image id must be non-zero."
			);
		}
		this.ImageId = imageId;
	}

	internal uint ImageId {
		get;
	}

	internal KittyGraphicsResponse? Response {
		get {
			lock ( this.sync ) {
				return this.response;
			}
		}
	}

	internal FormatException? Failure {
		get {
			lock ( this.sync ) {
				return this.failure;
			}
		}
	}

	internal bool CorrelationObserved {
		get {
			return 0 != Volatile.Read( ref this.correlationObserved );
		}
	}

	internal void RecordCorrelation() {
		Volatile.Write( ref this.correlationObserved, 1 );
	}

	internal void RecordResponse(
		KittyGraphicsResponse value
	) {
		ArgumentNullException.ThrowIfNull( value );
		if ( this.ImageId != value.ImageId ) {
			throw new ArgumentException(
				"The Kitty Graphics response does not match this support probe.",
				nameof( value )
			);
		}

		this.RecordCorrelation();
		lock ( this.sync ) {
			if ( this.response is not null || this.failure is not null ) {
				this.failure = new FormatException(
					"The terminal produced more than one correlated Kitty Graphics response for one support probe."
				);
				this.response = null;
				return;
			}
			this.response = value;
		}
	}

	internal void RecordFailure(
		FormatException exception
	) {
		ArgumentNullException.ThrowIfNull( exception );
		this.RecordCorrelation();
		lock ( this.sync ) {
			if ( this.response is not null || this.failure is not null ) {
				this.failure = new FormatException(
					"The terminal produced multiple correlated Kitty Graphics responses for one support probe.",
					exception
				);
				this.response = null;
				return;
			}
			this.failure = exception;
		}
	}
}
