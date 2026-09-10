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

			TerminalResponseFrameParseResult parseResult = TerminalResponseFramer.Parse(
				this.bufferedBytes,
				TerminalControlFamily.Apc,
				TerminalResponseFramer.DefaultMaximumFrameBytes
			);
			switch ( parseResult.Status ) {
				case TerminalResponseFrameParseStatus.NotCandidate:
				case TerminalResponseFrameParseStatus.Invalid:
					return false;

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
					return true;

				default:
					throw new InvalidOperationException(
						$"Unexpected terminal response framing status '{parseResult.Status}'."
					);
			}
		}
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
		if ( EscapeByte != bytes[ 0 ] || 2 > bytes.Count ) {
			return false;
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

	internal FormatException? Failure {
		get {
			lock ( this.sync ) {
				return this.failure;
			}
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
