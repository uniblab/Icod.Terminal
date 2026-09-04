namespace Icod.Terminal;

using System.Runtime.ExceptionServices;

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
		this.ValidateOutputEndpoint();

		HyperlinkState state = CreateState(
			uri,
			identifier
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
				state.BeginFrame,
				CancellationToken.None
			).ConfigureAwait( false );

			long leaseId = ++this.nextLeaseId;
			TerminalHyperlinkLease lease = new(
				this,
				leaseId,
				state.Uri,
				state.Identifier
			);
			this.stack.Add(
				new HyperlinkEntry(
					leaseId,
					state,
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
			if ( this.closed || 0 == this.stack.Count ) {
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
				: this.stack[ ^2 ].State.BeginFrame;

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
		this.ValidateOutputEndpoint();

		HyperlinkState state = CreateState(
			uri,
			identifier
		);
		byte[] textBytes = this.session.ApplicationEncoding.GetBytes( value );
		byte[] closeFrame = OscWriter.EncodeHyperlinkEndFrame();
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
				state.BeginFrame,
				CancellationToken.None
			).ConfigureAwait( false );

			long leaseId = ++this.nextLeaseId;
			TerminalHyperlinkLease lease = new(
				this,
				leaseId,
				state.Uri,
				state.Identifier
			);
			HyperlinkEntry entry = new(
				leaseId,
				state,
				lease
			);
			this.stack.Add( entry );

			Exception? applicationFailure = null;
			try {
				await this.session.Output.WriteAsync(
					textBytes,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch ( Exception exception ) {
				applicationFailure = exception;
			}

			Exception? releaseFailure = null;
			try {
				byte[] releaseFrame = 1 == this.stack.Count
					? closeFrame
					: this.stack[ ^2 ].State.BeginFrame;
				await this.session.Output.WriteAsync(
					releaseFrame,
					CancellationToken.None
				).ConfigureAwait( false );
				this.stack.RemoveAt( this.stack.Count - 1 );
				lease.MarkReleasedByOwner();
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
				ExceptionDispatchInfo.Capture( applicationFailure ).Throw();
			}
			if ( releaseFailure is not null ) {
				ExceptionDispatchInfo.Capture( releaseFailure ).Throw();
			}
		} finally {
			this.gate.Release();
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

	private void ValidateOutputEndpoint() {
		if ( !this.session.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 8 hyperlink operations require an interactive terminal output endpoint."
			);
		}
	}

	private void ThrowIfClosed() {
		if ( this.closed ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}

	private static HyperlinkState CreateState(
		string uri,
		string? identifier
	) {
		ArgumentNullException.ThrowIfNull( uri );

		string encodedUri = TerminalHyperlinkEncoder.EncodeUri( uri );
		string encodedParameters = TerminalHyperlinkEncoder.EncodeParameters( identifier );
		string? canonicalIdentifier = 0 == encodedParameters.Length
			? null
			: encodedParameters[ 3.. ];
		return new HyperlinkState(
			encodedUri,
			canonicalIdentifier,
			OscWriter.EncodeHyperlinkBeginFrame(
				encodedUri,
				canonicalIdentifier
			)
		);
	}

	private sealed record HyperlinkState(
		string Uri,
		string? Identifier,
		byte[] BeginFrame
	);

	private sealed record HyperlinkEntry(
		long LeaseId,
		HyperlinkState State,
		TerminalHyperlinkLease Lease
	);
}
