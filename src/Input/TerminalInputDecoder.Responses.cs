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
/// Response framing and expectation-driven demultiplexing for
/// <see cref="TerminalInputDecoder"/>.
/// </summary>
internal sealed partial class TerminalInputDecoder {
	private readonly object responseExpectationGate = new();
	private TerminalResponseExpectation? responseExpectation;

	internal TerminalResponseExpectation RegisterResponseExpectation(
		ITerminalResponseMatcher matcher
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		return this.RegisterResponseExpectation(
			matcher,
			armImmediately: true
		);
	}

	internal TerminalResponseExpectation RegisterResponseExpectation(
		ITerminalResponseMatcher matcher,
		bool armImmediately
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		return this.RegisterResponseExpectation(
			TerminalQueryResponsePlan.ForCompletion( matcher ),
			armImmediately
		);
	}

	internal TerminalResponseExpectation RegisterResponseExpectation(
		TerminalQueryResponsePlan responsePlan,
		bool armImmediately
	) {
		ArgumentNullException.ThrowIfNull( responsePlan );

		lock ( this.responseExpectationGate ) {
			if ( this.responseExpectation is not null ) {
				throw new InvalidOperationException(
					"The terminal input decoder already has an active response expectation."
				);
			}

			TerminalResponseExpectation expectation = new( responsePlan );
			if ( armImmediately ) {
				expectation.Arm( 0 );
			}
			this.responseExpectation = expectation;
			return expectation;
		}
	}

	internal void ArmResponseExpectation(
		TerminalResponseExpectation expectation
	) {
		ArgumentNullException.ThrowIfNull( expectation );

		lock ( this.responseExpectationGate ) {
			if ( !ReferenceEquals( this.responseExpectation, expectation ) ) {
				throw new InvalidOperationException(
					"The terminal response expectation is no longer active."
				);
			}

			expectation.Arm( this.bufferedBytes.Count );
		}
	}

	internal bool RemoveResponseExpectation(
		TerminalResponseExpectation expectation
	) {
		ArgumentNullException.ThrowIfNull( expectation );

		lock ( this.responseExpectationGate ) {
			if ( !ReferenceEquals( this.responseExpectation, expectation ) ) {
				return false;
			}

			this.responseExpectation = null;
		}

		expectation.TrySetCanceled();
		return true;
	}

	private async ValueTask<TerminalInputDecodeResult?> TryRouteExpectedResponseAsync(
		CancellationToken cancellationToken
	) {
		TerminalResponseExpectation? expectation = this.GetResponseExpectation();
		if ( expectation is null ) {
			return await this.TryDecodeModernKeyboardResultAsync(
				cancellationToken
			).ConfigureAwait( false );
		}
		if ( !expectation.IsArmed || 0 < expectation.ProtectedBufferedBytes ) {
			return await this.TryDecodeModernKeyboardResultAsync(
				cancellationToken
			).ConfigureAwait( false );
		}

		while ( true ) {
			if ( !ReferenceEquals(
				this.GetResponseExpectation(),
				expectation
			) ) {
				return await this.TryDecodeModernKeyboardResultAsync(
					cancellationToken
				).ConfigureAwait( false );
			}

			if ( await this.TryConsumeKittyKeyboardFlagsProbeAsync(
				cancellationToken
			).ConfigureAwait( false ) ) {
				continue;
			}

			TerminalResponseFrameParseResult parseResult = this.ParseExpectedResponse(
				expectation,
				out TerminalControlFamily? family,
				out int maximumFrameBytes
			);

			switch ( parseResult.Status ) {
				case TerminalResponseFrameParseStatus.NotCandidate:
					return await this.TryDecodeModernKeyboardResultAsync(
						cancellationToken
					).ConfigureAwait( false );

				case TerminalResponseFrameParseStatus.Invalid:
					if ( !family.HasValue ) {
						return await this.TryDecodeModernKeyboardResultAsync(
							cancellationToken
						).ConfigureAwait( false );
					}
					TerminalInputDecodeResult? oversizedResponse =
						await this.TryRouteOversizedCorrelatedResponseAsync(
							expectation,
							family.Value,
							maximumFrameBytes,
							cancellationToken
						).ConfigureAwait( false );
					if ( oversizedResponse.HasValue ) {
						return oversizedResponse;
					}
					return await this.TryDecodeModernKeyboardResultAsync(
						cancellationToken
					).ConfigureAwait( false );

				case TerminalResponseFrameParseStatus.Incomplete:
					bool appended = parseResult.IntroducerIncomplete
						&& EscapeByte == this.bufferedBytes[ 0 ]
							? await this.ReadMoreWithinEscapeWindowAsync(
								cancellationToken
							).ConfigureAwait( false )
							: await this.ReadMoreAsync(
								cancellationToken
							).ConfigureAwait( false )
					;
					if ( !appended ) {
						return await this.TryDecodeModernKeyboardResultAsync(
							cancellationToken
						).ConfigureAwait( false );
					}
					continue;

				case TerminalResponseFrameParseStatus.Complete:
					if ( !family.HasValue ) {
						throw new InvalidOperationException(
							"A complete terminal query response must identify its control family."
						);
					}
					TerminalResponseFrame frame = this.CreateResponseFrame(
						family.Value,
						parseResult.Length
					);
					if ( !expectation.ResponsePlan.TryMatch(
						frame,
						out TerminalQueryResponseDisposition disposition
					) ) {
						return await this.TryDecodeModernKeyboardResultAsync(
							cancellationToken
						).ConfigureAwait( false );
					}

					if ( !this.TryConsumeExpectedResponse(
						expectation,
						frame
					) ) {
						return await this.TryDecodeModernKeyboardResultAsync(
							cancellationToken
						).ConfigureAwait( false );
					}

					return TerminalInputDecodeResult.RoutedResponse(
						expectation,
						frame,
						disposition
					);

				default:
					throw new InvalidOperationException(
						$"Unexpected terminal response framing status '{parseResult.Status}'."
					);
			}
		}
	}

	private TerminalResponseFrameParseResult ParseExpectedResponse(
		TerminalResponseExpectation expectation,
		out TerminalControlFamily? family,
		out int maximumFrameBytes
	) {
		ArgumentNullException.ThrowIfNull( expectation );

		family = null;
		maximumFrameBytes = TerminalResponseFramer.DefaultMaximumFrameBytes;
		bool introducerIncomplete = false;
		IReadOnlyList<TerminalControlFamily> families = expectation.ResponsePlan.Families;
		for ( int index = 0; index < families.Count; index++ ) {
			TerminalControlFamily candidateFamily = families[ index ];
			TerminalQueryResponseRule rule = expectation.ResponsePlan.GetRule(
				candidateFamily
			);
			TerminalResponseFrameParseResult candidate = TerminalResponseFramer.Parse(
				this.bufferedBytes,
				candidateFamily,
				rule.MaximumFrameBytes
			);

			switch ( candidate.Status ) {
				case TerminalResponseFrameParseStatus.Complete:
				case TerminalResponseFrameParseStatus.Invalid:
					family = candidateFamily;
					maximumFrameBytes = rule.MaximumFrameBytes;
					return candidate;

				case TerminalResponseFrameParseStatus.Incomplete:
					maximumFrameBytes = Math.Max(
						maximumFrameBytes,
						rule.MaximumFrameBytes
					);
					if ( !candidate.IntroducerIncomplete ) {
						family = candidateFamily;
						return candidate;
					}
					introducerIncomplete = true;
					break;

				case TerminalResponseFrameParseStatus.NotCandidate:
					break;

				default:
					throw new InvalidOperationException(
						$"Unexpected terminal response framing status '{candidate.Status}'."
					);
			}
		}

		return introducerIncomplete
			? new TerminalResponseFrameParseResult(
				TerminalResponseFrameParseStatus.Incomplete,
				introducerIncomplete: true
			)
			: new TerminalResponseFrameParseResult(
				TerminalResponseFrameParseStatus.NotCandidate
			)
		;
	}

	private async ValueTask<TerminalInputDecodeResult?> TryDecodeModernKeyboardResultAsync(
		CancellationToken cancellationToken
	) {
		if ( 0 < this.pendingModernKeyboardEvents.Count ) {
			return TerminalInputDecodeResult.FromInput(
				this.pendingModernKeyboardEvents.Dequeue()
			);
		}

		TerminalInputEvent? inputEvent = await this.TryReadModernKeyboardEventAsync(
			cancellationToken
		).ConfigureAwait( false );
		if ( inputEvent is null ) {
			return null;
		}

		return TerminalInputDecodeResult.FromInput( inputEvent );
	}

	private async ValueTask<TerminalInputDecodeResult?> TryRouteOversizedCorrelatedResponseAsync(
		TerminalResponseExpectation expectation,
		TerminalControlFamily family,
		int maximumFrameBytes,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( expectation );
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException( nameof( family ) );
		}
		if ( this.bufferedBytes.Count < maximumFrameBytes
			|| !expectation.ResponsePlan.IsCorrelatedPrefix(
				family,
				this.bufferedBytes
			) ) {
			return null;
		}

		FormatException exception = new(
			$"The correlated terminal response exceeded the {maximumFrameBytes}-byte framing limit."
		);

		lock ( this.responseExpectationGate ) {
			if ( !ReferenceEquals( this.responseExpectation, expectation ) ) {
				return null;
			}

			this.responseExpectation = null;
			this.bufferedBytes.Clear();
		}

		expectation.TrySetException( exception );
		await this.DrainOversizedResponseAsync(
			family,
			cancellationToken
		).ConfigureAwait( false );

		return TerminalInputDecodeResult.RoutedFailure(
			expectation,
			exception
		);
	}

	private async ValueTask DrainOversizedResponseAsync(
		TerminalControlFamily family,
		CancellationToken cancellationToken
	) {
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException( nameof( family ) );
		}

		int discardedBytes = 0;
		while ( true ) {
			int terminatorLength = FindDiscardTerminator(
				this.bufferedBytes,
				family,
				out int terminatorIndex
			);
			if ( 0 < terminatorLength ) {
				this.Consume(
					terminatorIndex + terminatorLength
				);
				return;
			}

			int preserveBytes = 0 < this.bufferedBytes.Count
				&& EscapeByte == this.bufferedBytes[ ^1 ]
				&& TerminalControlFamily.Csi != family
					? 1
					: 0
			;
			int consumeCount = this.bufferedBytes.Count - preserveBytes;
			if ( 0 < consumeCount ) {
				discardedBytes = checked(
					discardedBytes + consumeCount
				);
				this.Consume( consumeCount );
				if ( TerminalResponseFramer.HardMaximumFrameBytes < discardedBytes ) {
					throw new InvalidOperationException(
						"The terminal input decoder could not resynchronize after an oversized response within the bounded discard interval."
					);
				}
			}

			if ( !await this.ReadMoreAsync(
				cancellationToken
			).ConfigureAwait( false ) ) {
				return;
			}
		}
	}

	private static int FindDiscardTerminator(
		IReadOnlyList<byte> bytes,
		TerminalControlFamily family,
		out int index
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException( nameof( family ) );
		}

		for ( int current = 0; current < bytes.Count; current++ ) {
			byte value = bytes[ current ];
			if ( TerminalControlFamily.Csi == family ) {
				if ( value is >= 0x40 and <= 0x7E ) {
					index = current;
					return 1;
				}
				continue;
			}

			if ( TerminalControlFamily.Osc == family && 0x07 == value ) {
				index = current;
				return 1;
			}
			if ( 0x9C == value ) {
				index = current;
				return 1;
			}
			if ( EscapeByte == value
				&& current + 1 < bytes.Count
				&& (byte)'\\' == bytes[ current + 1 ] ) {
				index = current;
				return 2;
			}
		}

		index = -1;
		return 0;
	}

	private TerminalResponseExpectation? GetResponseExpectation() {
		lock ( this.responseExpectationGate ) {
			return this.responseExpectation;
		}
	}

	private TerminalResponseFrame CreateResponseFrame(
		TerminalControlFamily family,
		int length
	) {
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException( nameof( family ) );
		}
		if ( 0 >= length || length > this.bufferedBytes.Count ) {
			throw new ArgumentOutOfRangeException( nameof( length ) );
		}

		return new TerminalResponseFrame(
			TerminalResponseFrameKinds.GetFrameKind( family ),
			this.bufferedBytes.GetRange(
				0,
				length
			).ToArray()
		);
	}

	private bool TryConsumeExpectedResponse(
		TerminalResponseExpectation expectation,
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( expectation );
		ArgumentNullException.ThrowIfNull( frame );

		lock ( this.responseExpectationGate ) {
			if ( !ReferenceEquals( this.responseExpectation, expectation ) ) {
				return false;
			}

			this.Consume( frame.Bytes.Length );
			this.responseExpectation = null;
		}

		return true;
	}
}
