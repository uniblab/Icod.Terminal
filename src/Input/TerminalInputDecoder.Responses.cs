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

	private async ValueTask<bool> TryRouteExpectedResponseAsync(
		CancellationToken cancellationToken
	) {
		TerminalResponseExpectation? expectation = this.GetResponseExpectation();
		if ( expectation is null
			|| !expectation.IsArmed
			|| 0 < expectation.ProtectedBufferedBytes ) {
			return false;
		}

		int maximumFrameBytes = Math.Min(
			TerminalResponseFramer.DefaultMaximumFrameBytes,
			this.maximumBufferedBytes
		);

		while ( true ) {
			if ( !ReferenceEquals(
				this.GetResponseExpectation(),
				expectation
			) ) {
				return false;
			}

			TerminalResponseFrameParseResult parseResult = TerminalResponseFramer.Parse(
				this.bufferedBytes,
				expectation.Matcher.FrameKind,
				maximumFrameBytes
			);

			switch ( parseResult.Status ) {
				case TerminalResponseFrameParseStatus.NotCandidate:
				case TerminalResponseFrameParseStatus.Invalid:
					return false;

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
						return false;
					}
					continue;

				case TerminalResponseFrameParseStatus.Complete:
					TerminalResponseFrame frame = this.CreateResponseFrame(
						expectation.Matcher.FrameKind,
						parseResult.Length
					);
					if ( !expectation.Matcher.IsMatch( frame ) ) {
						return false;
					}

					return this.TryConsumeExpectedResponse(
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

		expectation.TrySetResult( frame );
		return true;
	}
}
