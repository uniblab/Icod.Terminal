namespace Icod.Terminal;

using System.Runtime.ExceptionServices;

/// <summary>
/// Owns the strict-LIFO stack of session-managed OSC 8 hyperlink state.
/// </summary>
internal sealed class TerminalHyperlinkManager : ITerminalSessionLifecycleParticipant {
	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly List<HyperlinkEntry> stack = [];
	private readonly IDisposable lifecycleRegistration;

	private long nextLeaseId;
	private bool cleanupCloseRequired;
	private bool suspended;
	private bool closed;
	private int invalidated;

	internal TerminalHyperlinkManager(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		this.session = session;
		this.lifecycleRegistration = session.RegisterCoreLifecycleParticipant( this );
	}

	public ValueTask PrepareForTerminalSuspendAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.SuspendAsync();
	}

	public ValueTask ResumeAfterTerminalSuspendAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.ReenterAsync();
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

			if ( this.suspended ) {
				this.stack.Add( entry );
				return lease;
			}

			using IDisposable outputLease = await this.session.AcquireSessionOutputAsync(
				cancellationToken
			).ConfigureAwait( false );
			cancellationToken.ThrowIfCancellationRequested();
			await this.session.Output.WriteAsync(
				state.BeginFrame,
				CancellationToken.None
			).ConfigureAwait( false );

			this.cleanupCloseRequired = true;
			this.ClearInvalidated();
			this.stack.Add( entry );
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

			if ( this.suspended ) {
				this.stack.RemoveAt( this.stack.Count - 1 );
				current.Lease.MarkReleasedByOwner();
				return;
			}

			byte[] frame = 1 == this.stack.Count
				? OscWriter.EncodeHyperlinkEndFrame()
				: this.stack[ ^2 ].State.BeginFrame;

			try {
				using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
					CancellationToken.None
				).ConfigureAwait( false );
				await this.session.Output.WriteAsync(
					frame,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch {
				this.cleanupCloseRequired = true;
				this.MarkInvalidated();
				throw;
			}

			this.stack.RemoveAt( this.stack.Count - 1 );
			this.cleanupCloseRequired = 0 < this.stack.Count;
			this.ClearInvalidated();
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
		byte[] textBytes = this.session.EncodeApplicationText( value );
		byte[] closeFrame = OscWriter.EncodeHyperlinkEndFrame();
		cancellationToken.ThrowIfCancellationRequested();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Bounded OSC 8 hyperlink output cannot be emitted while terminal session state is suspended."
				);
			}
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

			this.cleanupCloseRequired = true;
			this.ClearInvalidated();

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
				this.cleanupCloseRequired = 0 < this.stack.Count;
				this.ClearInvalidated();
				lease.MarkReleasedByOwner();
			} catch ( Exception exception ) {
				this.cleanupCloseRequired = true;
				this.MarkInvalidated();
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

	internal void Invalidate() {
		this.MarkInvalidated();
	}

	internal async ValueTask SuspendAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed ) {
				return;
			}
			if ( this.suspended
				&& !this.IsInvalidated
				&& !this.cleanupCloseRequired ) {
				return;
			}

			if ( 0 == this.stack.Count && !this.cleanupCloseRequired ) {
				this.suspended = true;
				this.ClearInvalidated();
				return;
			}

			try {
				using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
					CancellationToken.None
				).ConfigureAwait( false );
				await this.session.Output.WriteAsync(
					OscWriter.EncodeHyperlinkEndFrame(),
					CancellationToken.None
				).ConfigureAwait( false );
				this.cleanupCloseRequired = false;
				this.suspended = true;
				this.ClearInvalidated();
			} catch {
				this.cleanupCloseRequired = true;
				this.suspended = true;
				this.MarkInvalidated();
				throw;
			}
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask ReenterAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed ) {
				return;
			}

			if ( 0 == this.stack.Count ) {
				if ( this.cleanupCloseRequired ) {
					try {
						using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
							CancellationToken.None
						).ConfigureAwait( false );
						await this.session.Output.WriteAsync(
							OscWriter.EncodeHyperlinkEndFrame(),
							CancellationToken.None
						).ConfigureAwait( false );
						this.cleanupCloseRequired = false;
					} catch {
						this.suspended = true;
						this.MarkInvalidated();
						throw;
					}
				}

				this.suspended = false;
				this.ClearInvalidated();
				return;
			}

			if ( !this.suspended && !this.IsInvalidated ) {
				return;
			}

			try {
				using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
					CancellationToken.None
				).ConfigureAwait( false );
				await this.session.Output.WriteAsync(
					this.stack[ ^1 ].State.BeginFrame,
					CancellationToken.None
				).ConfigureAwait( false );
				this.cleanupCloseRequired = true;
				this.suspended = false;
				this.ClearInvalidated();
			} catch {
				this.cleanupCloseRequired = true;
				this.suspended = true;
				this.MarkInvalidated();
				throw;
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
			bool shouldClose = this.cleanupCloseRequired
				|| ( !this.suspended && 0 < this.stack.Count );
			if ( shouldClose ) {
				try {
					using IDisposable outputLease = await this.session.AcquireControlOutputAsync(
						CancellationToken.None
					).ConfigureAwait( false );
					await this.session.Output.WriteAsync(
						OscWriter.EncodeHyperlinkEndFrame(),
						CancellationToken.None
					).ConfigureAwait( false );
					this.cleanupCloseRequired = false;
					this.ClearInvalidated();
				} catch ( Exception failure ) {
					this.cleanupCloseRequired = true;
					this.MarkInvalidated();
					exception = failure;
				}
			}

			this.closed = true;
			this.suspended = true;
			foreach ( HyperlinkEntry entry in this.stack ) {
				entry.Lease.MarkReleasedByOwner();
			}
			this.stack.Clear();

			if ( exception is not null ) {
				throw exception;
			}
		} finally {
			this.gate.Release();
			this.lifecycleRegistration.Dispose();
		}
	}

	private bool IsInvalidated {
		get {
			return 0 != Volatile.Read( ref this.invalidated );
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

	private void MarkInvalidated() {
		Volatile.Write( ref this.invalidated, 1 );
	}

	private void ClearInvalidated() {
		Volatile.Write( ref this.invalidated, 0 );
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
