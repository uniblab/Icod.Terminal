namespace Icod.Terminal;

/// <summary>
/// Owns the strict-LIFO stack of session-managed OSC 8 hyperlink state.
/// </summary>
internal sealed class TerminalHyperlinkManager {
	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly List<HyperlinkEntry> stack = [];

	private long nextLeaseId;
	private bool closed;

	internal TerminalHyperlinkManager(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		this.session = session;
	}

	internal async ValueTask<TerminalHyperlinkLease> AcquireAsync(
		string uri,
		string? identifier,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( uri );
		cancellationToken.ThrowIfCancellationRequested();

		if ( !this.session.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 8 hyperlink operations require an interactive terminal output endpoint."
			);
		}

		string encodedUri = TerminalHyperlinkEncoder.EncodeUri( uri );
		string encodedParameters = TerminalHyperlinkEncoder.EncodeParameters( identifier );
		string? canonicalIdentifier = 0 == encodedParameters.Length
			? null
			: encodedParameters[ 3.. ];
		byte[] beginFrame = OscWriter.EncodeHyperlinkBeginFrame(
			encodedUri,
			canonicalIdentifier
		);
		cancellationToken.ThrowIfCancellationRequested();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( long.MaxValue == this.nextLeaseId ) {
				throw new InvalidOperationException(
					"The terminal hyperlink lease identifier space has been exhausted."
				);
			}

			using IDisposable outputLease = await this.session.AcquireSessionOutputAsync(
				cancellationToken
			).ConfigureAwait( false );
			cancellationToken.ThrowIfCancellationRequested();
			await this.session.Output.WriteAsync(
				beginFrame,
				CancellationToken.None
			).ConfigureAwait( false );

			long leaseId = ++this.nextLeaseId;
			TerminalHyperlinkLease lease = new(
				this,
				leaseId,
				encodedUri,
				canonicalIdentifier
			);
			this.stack.Add(
				new HyperlinkEntry(
					leaseId,
					encodedUri,
					canonicalIdentifier,
					lease
				)
			);
			return lease;
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask ReleaseAsync(
		long leaseId
	) {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed ) {
				return;
			}
			if ( 0 == this.stack.Count ) {
				return;
			}

			HyperlinkEntry current = this.stack[ ^1 ];
			if ( current.LeaseId != leaseId ) {
				throw new InvalidOperationException(
					"Terminal hyperlink leases must be released in strict last-in, first-out order."
				);
			}

			byte[] frame = 1 == this.stack.Count
				? OscWriter.EncodeHyperlinkEndFrame()
				: OscWriter.EncodeHyperlinkBeginFrame(
					this.stack[ ^2 ].Uri,
					this.stack[ ^2 ].Identifier
				);

			using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false );
			await this.session.Output.WriteAsync(
				frame,
				CancellationToken.None
			).ConfigureAwait( false );

			this.stack.RemoveAt( this.stack.Count - 1 );
			current.Lease.MarkReleasedByOwner();
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask WriteBoundedAsync(
		string value,
		string uri,
		string? identifier,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( value );
		ArgumentNullException.ThrowIfNull( uri );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] textBytes = this.session.ApplicationEncoding.GetBytes( value );
		TerminalHyperlinkLease lease = await this.AcquireAsync(
			uri,
			identifier,
			cancellationToken
		).ConfigureAwait( false );

		Exception? applicationFailure = null;
		try {
			using IDisposable outputLease = await this.session.AcquireSessionOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false );
			await this.session.Output.WriteAsync(
				textBytes,
				CancellationToken.None
			).ConfigureAwait( false );
		} catch ( Exception exception ) {
			applicationFailure = exception;
		}

		Exception? releaseFailure = null;
		try {
			await lease.DisposeAsync().ConfigureAwait( false );
		} catch ( Exception exception ) {
			releaseFailure = exception;
		}

		if ( applicationFailure is not null && releaseFailure is not null ) {
			throw new AggregateException(
				"Hyperlink text output and OSC 8 cleanup both failed.",
				applicationFailure,
				releaseFailure
			);
		}
		if ( applicationFailure is not null ) {
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
				applicationFailure
			).Throw();
		}
		if ( releaseFailure is not null ) {
			System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(
				releaseFailure
			).Throw();
		}
	}

	internal async ValueTask CloseAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed ) {
				return;
			}

			Exception? exception = null;
			if ( 0 < this.stack.Count ) {
				try {
					using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
						CancellationToken.None
					).ConfigureAwait( false );
					await this.session.Output.WriteAsync(
						OscWriter.EncodeHyperlinkEndFrame(),
						CancellationToken.None
					).ConfigureAwait( false );
				} catch ( Exception failure ) {
					exception = failure;
				}
			}

			this.closed = true;
			foreach ( HyperlinkEntry entry in this.stack ) {
				entry.Lease.MarkReleasedByOwner();
			}
			this.stack.Clear();

			if ( exception is not null ) {
				throw exception;
			}
		} finally {
			this.gate.Release();
		}
	}

	private void ThrowIfClosed() {
		if ( this.closed ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}

	private sealed record HyperlinkEntry(
		long LeaseId,
		string Uri,
		string? Identifier,
		TerminalHyperlinkLease Lease
	);
}
