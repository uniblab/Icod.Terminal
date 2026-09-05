namespace Icod.Terminal;

/// <summary>
/// Owns strict-LIFO session-managed cursor-style state and truthful restoration.
/// </summary>
internal sealed class TerminalCursorStyleManager : ITerminalSessionLifecycleParticipant {
	private readonly TerminalSession session;
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly List<CursorStyleEntry> stack = [];
	private readonly IDisposable lifecycleRegistration;

	private TerminalCursorStyle? observedBaselineStyle;
	private long nextLeaseId;
	private bool restoreBaselineRequired;
	private bool suspended;
	private bool closed;

	internal TerminalCursorStyleManager(
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

	internal async ValueTask SetAsync(
		TerminalCursorStyle style,
		CancellationToken cancellationToken
	) {
		_ = TerminalCursorStyleCodec.GetParameter( style );
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateOutputEndpoint();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Cursor-style mutation is unavailable while terminal session state is suspended."
				);
			}
			if ( 0 < this.stack.Count ) {
				throw new InvalidOperationException(
					"Unscoped cursor-style mutation is unavailable while a cursor-style lease is active."
				);
			}
			if ( this.restoreBaselineRequired ) {
				throw new InvalidOperationException(
					"Cursor-style mutation is unavailable while baseline restoration remains pending."
				);
			}

			await this.EmitStyleAsync(
				style,
				cleanup: false,
				cancellationToken
			).ConfigureAwait( false );
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask<TerminalCursorStyleLease> AcquireAsync(
		TerminalCursorStyle style,
		TimeSpan timeout,
		CancellationToken cancellationToken
	) {
		_ = TerminalCursorStyleCodec.GetParameter( style );
		ValidateTimeout( timeout );
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateOutputEndpoint();

		await this.gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		try {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Cursor-style leases cannot be acquired while terminal session state is suspended."
				);
			}
			if ( this.restoreBaselineRequired && 0 == this.stack.Count ) {
				throw new InvalidOperationException(
					"A prior cursor-style baseline restoration is still pending."
				);
			}
			if ( long.MaxValue == this.nextLeaseId ) {
				throw new InvalidOperationException(
					"The terminal cursor-style lease identifier space has been exhausted."
				);
			}

			TerminalCursorStyle previousStyle;
			if ( 0 == this.stack.Count ) {
				TerminalCursorStyleObservation observation =
					await this.session.QueryCursorStyleAsync(
						timeout,
						cancellationToken
					).ConfigureAwait( false );
				if ( !observation.IsSupported ) {
					throw new NotSupportedException(
						"The terminal did not report DECRQSS cursor-style state, so exact cursor-style restoration cannot be guaranteed."
					);
				}

				previousStyle = observation.Style!.Value;
			} else {
				previousStyle = this.stack[ ^1 ].Style;
			}

			cancellationToken.ThrowIfCancellationRequested();
			await this.EmitStyleAsync(
				style,
				cleanup: false,
				cancellationToken
			).ConfigureAwait( false );

			long leaseId = ++this.nextLeaseId;
			TerminalCursorStyleLease lease = new(
				this,
				leaseId,
				style
			);
			this.stack.Add(
				new CursorStyleEntry(
					leaseId,
					style,
					previousStyle,
					lease
				)
			);
			if ( 1 == this.stack.Count ) {
				this.observedBaselineStyle = previousStyle;
			}
			this.restoreBaselineRequired = true;
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

			CursorStyleEntry current = this.stack[ ^1 ];
			if ( current.LeaseId != leaseId ) {
				throw new InvalidOperationException(
					"Terminal cursor-style leases must be released in strict last-in, first-out order."
				);
			}

			if ( this.suspended ) {
				this.stack.RemoveAt( this.stack.Count - 1 );
				current.Lease.MarkReleasedByOwner();
				if ( 0 == this.stack.Count && !this.restoreBaselineRequired ) {
					this.observedBaselineStyle = null;
				}
				return;
			}

			try {
				await this.EmitStyleAsync(
					current.PreviousStyle,
					cleanup: true,
					CancellationToken.None
				).ConfigureAwait( false );
			} catch {
				this.restoreBaselineRequired = true;
				throw;
			}

			this.stack.RemoveAt( this.stack.Count - 1 );
			current.Lease.MarkReleasedByOwner();
			this.restoreBaselineRequired = 0 < this.stack.Count;
			if ( 0 == this.stack.Count ) {
				this.observedBaselineStyle = null;
			}
		} finally {
			this.gate.Release();
		}
	}

	internal async ValueTask SuspendAsync() {
		await this.gate.WaitAsync( CancellationToken.None ).ConfigureAwait( false );
		try {
			if ( this.closed || this.suspended ) {
				return;
			}

			if ( 0 == this.stack.Count ) {
				this.suspended = true;
				return;
			}
			if ( !this.observedBaselineStyle.HasValue ) {
				throw new InvalidOperationException(
					"The observed cursor-style baseline is unavailable before suspension."
				);
			}

			try {
				await this.EmitStyleAsync(
					this.observedBaselineStyle.Value,
					cleanup: true,
					CancellationToken.None
				).ConfigureAwait( false );
				this.restoreBaselineRequired = false;
				this.suspended = true;
			} catch {
				this.restoreBaselineRequired = true;
				this.suspended = true;
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
				if ( this.restoreBaselineRequired && this.observedBaselineStyle.HasValue ) {
					await this.EmitStyleAsync(
						this.observedBaselineStyle.Value,
						cleanup: true,
						CancellationToken.None
					).ConfigureAwait( false );
					this.restoreBaselineRequired = false;
					this.observedBaselineStyle = null;
				}

				this.suspended = false;
				return;
			}

			try {
				await this.EmitStyleAsync(
					this.stack[ ^1 ].Style,
					cleanup: true,
					CancellationToken.None
				).ConfigureAwait( false );
				this.restoreBaselineRequired = true;
				this.suspended = false;
			} catch {
				this.restoreBaselineRequired = true;
				this.suspended = true;
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
			if ( this.restoreBaselineRequired && this.observedBaselineStyle.HasValue ) {
				try {
					await this.EmitStyleAsync(
						this.observedBaselineStyle.Value,
						cleanup: true,
						CancellationToken.None
					).ConfigureAwait( false );
					this.restoreBaselineRequired = false;
				} catch ( Exception failure ) {
					exception = failure;
				}
			}

			this.closed = true;
			this.suspended = true;
			foreach ( CursorStyleEntry entry in this.stack ) {
				entry.Lease.MarkReleasedByOwner();
			}
			this.stack.Clear();
			this.observedBaselineStyle = null;

			if ( exception is not null ) {
				throw exception;
			}
		} finally {
			this.gate.Release();
			this.lifecycleRegistration.Dispose();
		}
	}

	private async ValueTask EmitStyleAsync(
		TerminalCursorStyle style,
		bool cleanup,
		CancellationToken cancellationToken
	) {
		int parameter = TerminalCursorStyleCodec.GetParameter( style );
		IDisposable outputLease;
		if ( cleanup ) {
			outputLease = await this.session.AcquireControlOutputAsync(
				CancellationToken.None
			).ConfigureAwait( false );
		} else {
			outputLease = await this.session.AcquireSessionOutputAsync(
				cancellationToken
			).ConfigureAwait( false );
		}

		using ( outputLease ) {
			await CsiWriter.WriteCursorStyleAsync(
				this.session.Output,
				parameter,
				cleanup ? CancellationToken.None : cancellationToken
			).ConfigureAwait( false );
		}
	}

	private void ValidateOutputEndpoint() {
		if ( !this.session.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Cursor-style operations require an interactive terminal output endpoint."
			);
		}
	}

	private void ThrowIfClosed() {
		if ( this.closed ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}

	private static void ValidateTimeout(
		TimeSpan timeout
	) {
		if ( TimeSpan.Zero > timeout
			|| TerminalQueryTransactionManager.MaximumCallerTimeout < timeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( timeout ),
				timeout,
				"A terminal query timeout must be between zero and "
					+ $"{TerminalQueryTransactionManager.MaximumCallerTimeout}."
			);
		}
	}

	private sealed record CursorStyleEntry(
		long LeaseId,
		TerminalCursorStyle Style,
		TerminalCursorStyle PreviousStyle,
		TerminalCursorStyleLease Lease
	);
}
