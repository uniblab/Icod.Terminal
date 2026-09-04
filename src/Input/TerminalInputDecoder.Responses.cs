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

		lock ( this.responseExpectationGate ) {
			if ( this.responseExpectation is not null ) {
				throw new InvalidOperationException(
					"The terminal input decoder already has an active response expectation."
				);
			}

			TerminalResponseExpectation expectation = new( matcher );
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
		if ( expectation is null
			|| !expectation.IsArmed
			|| 0 < expectation.ProtectedBufferedBytes ) {
			return null;
		}

		int framingLimit = TerminalResponseFrameKind.Osc == expectation.Matcher.FrameKind
			? TerminalOsc52PayloadCodec.MaximumFrameBytes
			: TerminalResponseFramer.DefaultMaximumFrameBytes
		;
		int maximumFrameBytes = Math.Min(
			framingLimit,
			this.maximumBufferedBytes
		);

		while ( true ) {
			if ( !ReferenceEquals(
				this.GetResponseExpectation(),
				expectation
			) ) {
				return null;
			}

			TerminalResponseFrameParseResult parseResult = TerminalResponseFramer.Parse(
				this.bufferedBytes,
				expectation.Matcher.FrameKind,
				maximumFrameBytes
			);

			switch ( parseResult.Status ) {
				case TerminalResponseFrameParseStatus.NotCandidate:
					return null;

				case TerminalResponseFrameParseStatus.Invalid:
					TerminalInputDecodeResult? invalidResponse =
						await this.TryRouteCorrelatedInvalidResponseAsync(
							expectation,
							maximumFrameBytes,
							cancellationToken
						).ConfigureAwait( false );
					return invalidResponse;

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
						return null;
					}
					continue;

				case TerminalResponseFrameParseStatus.Complete:
					TerminalResponseFrame frame = this.CreateResponseFrame(
						expectation.Matcher.FrameKind,
						parseResult.Length
					);
					if ( !expectation.Matcher.IsMatch( frame ) ) {
						return null;
					}

					if ( !this.TryConsumeExpectedResponse(
						expectation,
						frame
					) ) {
						return null;
					}

					return TerminalInputDecodeResult.RoutedResponse(
						expectation,
						frame
					);

				default:
					throw new InvalidOperationException(
						$"Unexpected terminal response framing status '{parseResult.Status}'."
					);
			}
		}
	}

	private async ValueTask<TerminalInputDecodeResult?> TryRouteCorrelatedInvalidResponseAsync(
		TerminalResponseExpectation expectation,
		int maximumFrameBytes,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( expectation );
		if ( expectation.Matcher is not ICorrelatedTerminalResponseMatcher correlatedMatcher
			|| !correlatedMatcher.IsCorrelatedPrefix( this.bufferedBytes ) ) {
			return null;
		}

		bool oversized = maximumFrameBytes <= this.bufferedBytes.Count;
		FormatException exception = new(
			oversized
				? $"The correlated terminal response exceeded the {maximumFrameBytes}-byte framing limit."
				: "The correlated terminal response used malformed framing."
		);

		lock ( this.responseExpectationGate ) {
			if ( !ReferenceEquals( this.responseExpectation, expectation ) ) {
				return null;
			}

			this.responseExpectation = null;
			this.bufferedBytes.Clear();
		}

		expectation.TrySetException( exception );
		await this.DrainInvalidOscResponseAsync(
			cancellationToken
		).ConfigureAwait( false );

		return TerminalInputDecodeResult.RoutedFailure(
			expectation,
			exception
		);
	}

	private async ValueTask DrainInvalidOscResponseAsync(
		CancellationToken cancellationToken
	) {
		int discardedBytes = 0;

		while ( true ) {
			int terminatorLength = FindOscDiscardTerminator(
				this.bufferedBytes,
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
					? 1
					: 0
			;
			int consumeCount = this.bufferedBytes.Count - preserveBytes;
			if ( 0 < consumeCount ) {
				discardedBytes = checked(
					discardedBytes + consumeCount
				);
				this.Consume( consumeCount );
				if ( TerminalOsc52PayloadCodec.MaximumFrameBytes < discardedBytes ) {
					throw new InvalidOperationException(
						"The terminal input decoder could not resynchronize after an invalid OSC response within the bounded discard interval."
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

	private static int FindOscDiscardTerminator(
		IReadOnlyList<byte> bytes,
		out int index
	) {
		ArgumentNullException.ThrowIfNull( bytes );

		for ( int current = 0; current < bytes.Count; current++ ) {
			byte value = bytes[ current ];
			if ( 0x07 == value || 0x9C == value ) {
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
		TerminalResponseFrameKind kind,
		int length
	) {
		if ( 0 >= length || length > this.bufferedBytes.Count ) {
			throw new ArgumentOutOfRangeException( nameof( length ) );
		}

		return new TerminalResponseFrame(
			kind,
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
