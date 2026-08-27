namespace Icod.Terminal;

using System.Threading.Channels;
using Icod.TermInfo;

/// <summary>
/// Live dimensions and process/terminal lifecycle coordination for
/// <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private readonly object lifecycleSync = new();
	private readonly CancellationTokenSource lifecycleStop = new();
	private readonly CancellationTokenSource termination = new();
	private readonly Channel<TerminalLifecycleEvent> lifecycleEvents =
		Channel.CreateUnbounded<TerminalLifecycleEvent>(
			new UnboundedChannelOptions {
				SingleReader = false,
				SingleWriter = true,
				AllowSynchronousContinuations = false
			}
		);
	private readonly ITerminalLifecycleSource? lifecycleSource;

	private Task? lifecyclePumpTask;
	private Task? lifecycleStopTask;
	private TerminalSize? lastResizeNotificationSize;
	private int lifecycleStateReleased;

	/// <summary>
	/// Gets whether this session has automatic host lifecycle observation.
	/// </summary>
	public bool SupportsLifecycleEvents {
		get {
			return this.lifecycleSource is not null;
		}
	}

	/// <summary>
	/// Gets a token canceled after an interactive interrupt, termination request,
	/// or fatal lifecycle-pump failure is observed.
	/// </summary>
	public CancellationToken TerminationToken {
		get {
			return this.termination.Token;
		}
	}

	/// <summary>
	/// Queries the current live terminal dimensions using the best observed session endpoint.
	/// </summary>
	/// <returns>The controlled live-size result.</returns>
	public TerminalControlResult<TerminalSize> GetSize() {
		TerminalControlResult<TerminalSize>? firstFailure = null;

		if ( SupportsLiveSize( this.OutputObservation ) ) {
			TerminalControlResult<TerminalSize> outputResult =
				this.controlProvider.GetSize( this.OutputEndpoint );
			if ( outputResult.IsAvailable ) {
				return outputResult;
			}

			firstFailure = outputResult;
		}

		if ( SupportsLiveSize( this.InputObservation )
			&& !ReferenceEquals( this.InputEndpoint, this.OutputEndpoint ) ) {
			TerminalControlResult<TerminalSize> inputResult =
				this.controlProvider.GetSize( this.InputEndpoint );
			if ( inputResult.IsAvailable ) {
				return inputResult;
			}

			firstFailure ??= inputResult;
		}

		return firstFailure
			?? TerminalControlResult<TerminalSize>.Unavailable(
				"Neither observed session endpoint advertises live terminal dimensions."
			);
	}

	/// <summary>
	/// Waits for the next normalized terminal or process lifecycle event.
	/// </summary>
	/// <param name="cancellationToken">Cancellation for this wait only.</param>
	/// <returns>The next lifecycle event.</returns>
	public ValueTask<TerminalLifecycleEvent> ReadLifecycleEventAsync(
		CancellationToken cancellationToken = default
	) {
		if ( this.lifecycleSource is null ) {
			throw new NotSupportedException(
				"Automatic lifecycle observation is unavailable for this terminal session."
			);
		}

		return this.lifecycleEvents.Reader.ReadAsync( cancellationToken );
	}

	private static bool SupportsLiveSize(
		TerminalEndpointObservation observation
	) {
		ArgumentNullException.ThrowIfNull( observation );

		return observation.IsTerminal
			&& 0 != ( observation.Capabilities & TerminalControlCapabilities.LiveSize );
	}

	private static ITerminalLifecycleSource? ResolveLifecycleSource(
		ITerminalControlProvider controlProvider,
		TerminalSessionOptions options
	) {
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( options );

		if ( options.LifecycleSource is not null ) {
			return options.LifecycleSource;
		}
		if ( !options.ObserveLifecycleEvents
			|| !ReferenceEquals(
				controlProvider,
				SystemTerminalControlProvider.Instance
			) ) {
			return null;
		}

		return SystemTerminalLifecycleSource.TryCreate();
	}

	private void StartLifecyclePump() {
		if ( this.lifecycleSource is null ) {
			return;
		}

		lock ( this.lifecycleSync ) {
			this.lifecyclePumpTask ??= Task.Run( this.RunLifecyclePumpAsync );
		}
	}

	private ValueTask StopLifecycleAsync() {
		lock ( this.lifecycleSync ) {
			this.lifecycleStopTask ??= this.StopLifecycleOnceAsync();
			return new ValueTask( this.lifecycleStopTask );
		}
	}

	private async Task StopLifecycleOnceAsync() {
		this.lifecycleStop.Cancel();

		Exception? disposalException = null;
		try {
			this.lifecycleSource?.Dispose();
		} catch ( Exception exception ) {
			disposalException = exception;
		}

		Task? pumpTask;
		lock ( this.lifecycleSync ) {
			pumpTask = this.lifecyclePumpTask;
		}

		if ( pumpTask is not null ) {
			await pumpTask.ConfigureAwait( false );
		}

		if ( disposalException is not null ) {
			this.TryCancelTermination();
			this.lifecycleEvents.Writer.TryComplete( disposalException );
		} else {
			this.lifecycleEvents.Writer.TryComplete();
		}
	}

	private async Task RunLifecyclePumpAsync() {
		try {
			while ( true ) {
				this.lifecycleStop.Token.ThrowIfCancellationRequested();

				TerminalLifecycleSignal signal = await this.lifecycleSource!.ReadAsync(
					this.lifecycleStop.Token
				).ConfigureAwait( false );

				await this.HandleLifecycleSignalAsync( signal ).ConfigureAwait( false );
			}
		} catch ( OperationCanceledException ) when ( this.lifecycleStop.IsCancellationRequested ) {
		} catch ( ChannelClosedException ) when ( this.lifecycleStop.IsCancellationRequested ) {
		} catch ( Exception exception ) {
			this.TryCancelTermination();
			this.lifecycleEvents.Writer.TryComplete( exception );
		}
	}

	private async ValueTask HandleLifecycleSignalAsync(
		TerminalLifecycleSignal signal
	) {
		switch ( signal.Kind ) {
			case TerminalLifecycleSignalKind.Resize:
				this.HandleResizeSignal();
				break;

			case TerminalLifecycleSignalKind.Interrupt:
				this.TryCancelTermination();
				this.PublishLifecycleEvent( TerminalLifecycleEventKind.Interrupt );
				break;

			case TerminalLifecycleSignalKind.Termination:
				this.TryCancelTermination();
				this.PublishLifecycleEvent( TerminalLifecycleEventKind.Termination );
				break;

			case TerminalLifecycleSignalKind.Suspend:
				await this.HandleSuspendAsync().ConfigureAwait( false );
				break;

			case TerminalLifecycleSignalKind.Resume:
				await this.HandleResumeAsync().ConfigureAwait( false );
				break;

			default:
				throw new ArgumentOutOfRangeException(
					nameof( signal ),
					signal.Kind,
					"The terminal lifecycle signal is not recognized."
				);
		}
	}

	private async ValueTask HandleSuspendAsync() {
		await this.LeaveSessionStateForSuspendAsync().ConfigureAwait( false );
		this.PublishLifecycleEvent( TerminalLifecycleEventKind.Suspending );

		if ( this.lifecycleSource is not ITerminalSuspendController suspendController ) {
			Exception suspensionException = new NotSupportedException(
				"The terminal lifecycle source cannot complete process suspension."
			);
			await this.ThrowAfterReentryAttemptAsync( suspensionException ).ConfigureAwait( false );
			return;
		}

		TerminalControlMutationResult result = suspendController.SuspendCurrentProcess();
		if ( result.Succeeded ) {
			return;
		}

		Exception failure = new InvalidOperationException(
			result.Message
				?? "The process could not be suspended after terminal-state restoration."
		);
		await this.ThrowAfterReentryAttemptAsync( failure ).ConfigureAwait( false );
	}

	private async ValueTask HandleResumeAsync() {
		this.InvalidateState();
		await this.ReapplySessionStateAsync().ConfigureAwait( false );

		TerminalSize? size = this.TryGetAvailableSize();
		this.lastResizeNotificationSize = size;
		this.PublishLifecycleEvent(
			TerminalLifecycleEventKind.Resumed,
			size
		);
	}

	private async ValueTask LeaveSessionStateForSuspendAsync() {
		if ( 0 != Interlocked.CompareExchange(
			ref this.lifecycleStateReleased,
			1,
			0
		) ) {
			return;
		}

		Volatile.Write( ref this.stateValid, 0 );
		List<Exception> exceptions = [];

		try {
			await this.PrepareLifecycleParticipantsAsync().ConfigureAwait( false );
		} catch ( Exception exception ) {
			exceptions.Add( exception );
		}

		try {
			await this.SuspendInputProtocolStateAsync().ConfigureAwait( false );
		} catch ( Exception exception ) {
			exceptions.Add( exception );
		}

		try {
			await this.SuspendPresentationStateAsync().ConfigureAwait( false );
		} catch ( Exception exception ) {
			exceptions.Add( exception );
		}

		try {
			await this.Output.FlushAsync( CancellationToken.None ).ConfigureAwait( false );
		} catch ( Exception exception ) {
			exceptions.Add( exception );
		}

		this.DisposeOutputModeLease( exceptions );

		if ( this.baselineMode is null ) {
			exceptions.Add(
				new InvalidOperationException(
					"The captured terminal baseline is unavailable before suspension."
				)
			);
		} else if ( this.restoreRequired ) {
			try {
				TerminalControlMutationResult result = this.controlProvider.SetMode(
					this.InputEndpoint,
					this.baselineMode,
					GetRestoreTiming( this.baselineMode )
				);

				if ( result.Succeeded ) {
					this.restoreRequired = false;
				} else {
					exceptions.Add(
						new InvalidOperationException(
							result.Message
								?? "The terminal baseline could not be restored before suspension."
						)
					);
				}
			} catch ( Exception exception ) {
				exceptions.Add( exception );
			}
		}

		Exception? leaveException = BuildRestorationException( exceptions );
		if ( leaveException is null ) {
			return;
		}

		await this.ThrowAfterReentryAttemptAsync( leaveException ).ConfigureAwait( false );
	}

	private async ValueTask ReapplySessionStateAsync() {
		Volatile.Write( ref this.stateValid, 0 );

		try {
			IDisposable? priorOutputModeLease = this.outputModeLease;
			this.outputModeLease = null;
			priorOutputModeLease?.Dispose();

			this.outputModeLease = SystemTerminalOutputSetup.Configure(
				this.controlProvider,
				this.OutputEndpoint,
				this.OutputObservation,
				this.Options.ConfigureOutput
			);

			if ( this.baselineMode is null ) {
				throw new InvalidOperationException(
					"The captured terminal baseline is unavailable during lifecycle re-entry."
				);
			}

			this.restoreRequired = true;
			TerminalControlMutationResult result = TerminalInputModePolicy.Apply(
				this.controlProvider,
				this.InputEndpoint,
				this.baselineMode,
				this.Options.InputMode,
				this.Options.EchoInput
			);
			if ( !result.Succeeded ) {
				throw new InvalidOperationException(
					result.Message
						?? "The requested terminal input mode could not be reapplied after resume."
				);
			}

			await this.ResumePresentationStateAsync().ConfigureAwait( false );
			await this.ResumeInputProtocolStateAsync().ConfigureAwait( false );
			await this.ResumeLifecycleParticipantsAsync().ConfigureAwait( false );

			Interlocked.Exchange( ref this.lifecycleStateReleased, 0 );
			Volatile.Write( ref this.stateValid, 1 );
			return;
		} catch ( Exception exception ) {
			Volatile.Write( ref this.stateValid, 0 );
			Interlocked.Exchange( ref this.lifecycleStateReleased, 1 );

			List<Exception> rollbackExceptions = [];
			try {
				await this.SuspendInputProtocolStateAsync().ConfigureAwait( false );
			} catch ( Exception rollbackException ) {
				rollbackExceptions.Add( rollbackException );
			}
			try {
				await this.SuspendPresentationStateAsync().ConfigureAwait( false );
			} catch ( Exception rollbackException ) {
				rollbackExceptions.Add( rollbackException );
			}

			this.DisposeOutputModeLease( rollbackExceptions );
			this.TryRestoreBaselineAfterFailedReentry( rollbackExceptions );

			Exception? rollbackException = BuildRestorationException( rollbackExceptions );
			if ( rollbackException is not null ) {
				throw new AggregateException(
					"Terminal lifecycle re-entry failed and rollback also reported an error.",
					exception,
					rollbackException
				);
			}

			throw;
		}
	}

	private void TryRestoreBaselineAfterFailedReentry(
		ICollection<Exception> exceptions
	) {
		ArgumentNullException.ThrowIfNull( exceptions );

		if ( !this.restoreRequired || ( this.baselineMode is null ) ) {
			return;
		}

		try {
			TerminalControlMutationResult result = this.controlProvider.SetMode(
				this.InputEndpoint,
				this.baselineMode,
				GetRestoreTiming( this.baselineMode )
			);
			if ( result.Succeeded ) {
				this.restoreRequired = false;
				return;
			}

			exceptions.Add(
				new InvalidOperationException(
					result.Message
						?? "The terminal baseline could not be restored after failed lifecycle re-entry."
				)
			);
		} catch ( Exception exception ) {
			exceptions.Add( exception );
		}
	}

	private void DisposeOutputModeLease(
		ICollection<Exception> exceptions
	) {
		ArgumentNullException.ThrowIfNull( exceptions );

		try {
			this.outputModeLease?.Dispose();
		} catch ( Exception exception ) {
			exceptions.Add( exception );
		} finally {
			this.outputModeLease = null;
		}
	}

	private async ValueTask ThrowAfterReentryAttemptAsync(
		Exception primaryException
	) {
		ArgumentNullException.ThrowIfNull( primaryException );

		try {
			await this.ReapplySessionStateAsync().ConfigureAwait( false );
		} catch ( Exception reentryException ) {
			throw new AggregateException(
				"Terminal lifecycle handling failed and session-state re-entry also failed.",
				primaryException,
				reentryException
			);
		}

		throw primaryException;
	}

	private void HandleResizeSignal() {
		TerminalSize? size = this.TryGetAvailableSize();
		if ( size.HasValue
			&& this.lastResizeNotificationSize.HasValue
			&& size.Value == this.lastResizeNotificationSize.Value ) {
			return;
		}

		this.lastResizeNotificationSize = size;
		this.PublishLifecycleEvent(
			TerminalLifecycleEventKind.Resize,
			size
		);
	}

	private TerminalSize? TryGetAvailableSize() {
		TerminalControlResult<TerminalSize> result = this.GetSize();
		return result.IsAvailable
			? result.GetRequiredValue()
			: null;
	}

	private void PublishLifecycleEvent(
		TerminalLifecycleEventKind kind,
		TerminalSize? size = null
	) {
		this.lifecycleEvents.Writer.TryWrite(
			new TerminalLifecycleEvent(
				kind,
				size
			)
		);
	}

	private void TryCancelTermination() {
		try {
			this.termination.Cancel();
		} catch ( ObjectDisposedException ) {
		}
	}
}
