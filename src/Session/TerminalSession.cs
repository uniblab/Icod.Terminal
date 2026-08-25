namespace Icod.Terminal;

using System.Runtime.ExceptionServices;

/// <summary>
/// Owns one reversible live-terminal input-mode transition while borrowing the
/// terminal endpoints and byte transports supplied by the caller.
/// </summary>
/// <remarks>
/// A session owns terminal state transitions, not endpoint or stream lifetime.
/// Dispose the session asynchronously to flush pending output and restore the
/// captured input mode exactly once.
/// </remarks>
public sealed class TerminalSession : IAsyncDisposable {
	private readonly object restoreSync = new();
	private readonly ITerminalControlProvider controlProvider;

	private TerminalModeSnapshot? baselineMode;
	private Task<Exception?>? restoreTask;
	private bool restoreRequired;
	private int stateValid;

	private TerminalSession(
		ITerminalControlProvider controlProvider,
		TerminalEndpoint inputEndpoint,
		TerminalEndpoint outputEndpoint,
		TerminalEndpointObservation inputObservation,
		TerminalEndpointObservation outputObservation,
		ITerminalInput input,
		ITerminalOutput output,
		TerminalSessionOptions options
	) {
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( inputEndpoint );
		ArgumentNullException.ThrowIfNull( outputEndpoint );
		ArgumentNullException.ThrowIfNull( inputObservation );
		ArgumentNullException.ThrowIfNull( outputObservation );
		ArgumentNullException.ThrowIfNull( input );
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( options );

		this.controlProvider = controlProvider;
		this.InputEndpoint = inputEndpoint;
		this.OutputEndpoint = outputEndpoint;
		this.InputObservation = inputObservation;
		this.OutputObservation = outputObservation;
		this.Input = input;
		this.Output = output;
		this.Options = options;
	}

	/// <summary>Gets the terminal input endpoint borrowed by the session.</summary>
	public TerminalEndpoint InputEndpoint {
		get;
	}

	/// <summary>Gets the terminal output endpoint borrowed by the session.</summary>
	public TerminalEndpoint OutputEndpoint {
		get;
	}

	/// <summary>Gets the attachment observation captured for the input endpoint.</summary>
	public TerminalEndpointObservation InputObservation {
		get;
	}

	/// <summary>Gets the attachment observation captured for the output endpoint.</summary>
	public TerminalEndpointObservation OutputObservation {
		get;
	}

	/// <summary>Gets the borrowed terminal input byte service.</summary>
	public ITerminalInput Input {
		get;
	}

	/// <summary>Gets the borrowed terminal output byte service.</summary>
	public ITerminalOutput Output {
		get;
	}

	/// <summary>Gets the immutable options with which this session was opened.</summary>
	public TerminalSessionOptions Options {
		get;
	}

	/// <summary>Gets whether both observed endpoints are interactive terminals.</summary>
	public bool IsInteractive {
		get {
			return this.InputObservation.IsTerminal
				&& this.OutputObservation.IsTerminal;
		}
	}

	/// <summary>
	/// Gets whether the session still considers its applied terminal state valid.
	/// </summary>
	/// <remarks>
	/// External suspension, console reconfiguration, or other out-of-band terminal
	/// changes may invalidate state. T07 will use this marker when coordinating
	/// lifecycle-driven re-entry.
	/// </remarks>
	public bool IsStateValid {
		get {
			return 0 != Volatile.Read( ref this.stateValid );
		}
	}

	/// <summary>
	/// Opens a session against process standard input and standard output.
	/// </summary>
	/// <param name="options">Optional input-mode and interactivity policy.</param>
	/// <param name="cancellationToken">Cancellation for session initialization.</param>
	/// <returns>The initialized terminal session.</returns>
	public static ValueTask<TerminalSession> OpenAsync(
		TerminalSessionOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();

		return OpenAsync(
			SystemTerminalControlProvider.Instance,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new StreamTerminalInput( Console.OpenStandardInput() ),
			new StreamTerminalOutput( Console.OpenStandardOutput() ),
			options,
			cancellationToken
		);
	}

	/// <summary>
	/// Opens a session against explicitly supplied terminal endpoints and byte services.
	/// </summary>
	/// <param name="controlProvider">The terminal-control provider.</param>
	/// <param name="inputEndpoint">The terminal input endpoint to observe and configure.</param>
	/// <param name="outputEndpoint">The terminal output endpoint to observe.</param>
	/// <param name="input">The borrowed input byte service.</param>
	/// <param name="output">The borrowed output byte service.</param>
	/// <param name="options">Optional input-mode and interactivity policy.</param>
	/// <param name="cancellationToken">Cancellation for session initialization.</param>
	/// <returns>The initialized terminal session.</returns>
	/// <remarks>
	/// The session never disposes the supplied provider, endpoints, input service,
	/// or output service. Their lifetime remains with the caller.
	/// </remarks>
	public static async ValueTask<TerminalSession> OpenAsync(
		ITerminalControlProvider controlProvider,
		TerminalEndpoint inputEndpoint,
		TerminalEndpoint outputEndpoint,
		ITerminalInput input,
		ITerminalOutput output,
		TerminalSessionOptions? options = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( inputEndpoint );
		ArgumentNullException.ThrowIfNull( outputEndpoint );
		ArgumentNullException.ThrowIfNull( input );
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalSessionOptions resolvedOptions =
			options
			?? new TerminalSessionOptions();
		resolvedOptions.Validate();

		TerminalEndpointObservation inputObservation = GetObservation(
			controlProvider,
			inputEndpoint,
			"input"
		);
		if ( !inputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				string.Concat(
					"Terminal session input '",
					inputEndpoint.DisplayName,
					"' is not an interactive terminal."
				)
			);
		}

		TerminalEndpointObservation outputObservation = GetObservation(
			controlProvider,
			outputEndpoint,
			"output"
		);
		if ( resolvedOptions.RequireInteractiveOutput
			&& !outputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				string.Concat(
					"Terminal session output '",
					outputEndpoint.DisplayName,
					"' is not an interactive terminal."
				)
			);
		}

		TerminalSession session = new(
			controlProvider,
			inputEndpoint,
			outputEndpoint,
			inputObservation,
			outputObservation,
			input,
			output,
			resolvedOptions
		);

		try {
			await session.InitializeAsync( cancellationToken ).ConfigureAwait( false );
			return session;
		} catch ( Exception exception ) {
			Exception? restorationException =
				await session.RestoreCoreAsync().ConfigureAwait( false );

			if ( restorationException is not null ) {
				throw new AggregateException(
					"Terminal session initialization failed and restoration also reported an error.",
					exception,
					restorationException
				);
			}

			throw;
		}
	}

	/// <summary>
	/// Marks the currently applied terminal state invalid after out-of-band host activity.
	/// </summary>
	/// <remarks>
	/// Invalidation does not restore or reapply terminal state. It records that the
	/// session can no longer assume its configured state is still active. Lifecycle
	/// re-entry is introduced in T07.
	/// </remarks>
	public void InvalidateState() {
		Volatile.Write( ref this.stateValid, 0 );
	}

	/// <summary>
	/// Flushes pending output and restores the captured input mode exactly once.
	/// </summary>
	/// <returns>A value task representing asynchronous restoration.</returns>
	public async ValueTask DisposeAsync() {
		Exception? exception =
			await this.RestoreCoreAsync().ConfigureAwait( false );

		if ( exception is not null ) {
			ExceptionDispatchInfo.Capture( exception ).Throw();
		}
	}

	private static TerminalEndpointObservation GetObservation(
		ITerminalControlProvider controlProvider,
		TerminalEndpoint endpoint,
		string role
	) {
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( endpoint );
		ArgumentException.ThrowIfNullOrWhiteSpace( role );

		TerminalControlResult<TerminalEndpointObservation> result =
			controlProvider.Observe( endpoint );
		if ( !result.IsAvailable ) {
			throw new InvalidOperationException(
				result.Message
				?? string.Concat(
					"The terminal ",
					role,
					" endpoint could not be observed."
				)
			);
		}

		return result.GetRequiredValue();
	}

	private ValueTask InitializeAsync(
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();

		TerminalControlResult<TerminalModeSnapshot> captureResult =
			this.controlProvider.GetMode( this.InputEndpoint );
		if ( !captureResult.IsAvailable ) {
			throw new InvalidOperationException(
				captureResult.Message
				?? "The terminal input mode could not be captured."
			);
		}

		this.baselineMode = captureResult.GetRequiredValue();
		this.restoreRequired = true;
		cancellationToken.ThrowIfCancellationRequested();

		TerminalControlMutationResult applyResult = TerminalInputModePolicy.Apply(
			this.controlProvider,
			this.InputEndpoint,
			this.baselineMode,
			this.Options.InputMode,
			this.Options.EchoInput
		);
		if ( !applyResult.Succeeded ) {
			throw new InvalidOperationException(
				applyResult.Message
				?? "The requested terminal input mode could not be applied."
			);
		}

		Volatile.Write( ref this.stateValid, 1 );
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}

	private ValueTask<Exception?> RestoreCoreAsync() {
		lock ( this.restoreSync ) {
			this.restoreTask ??= this.RestoreOnceAsync();
			return new ValueTask<Exception?>( this.restoreTask );
		}
	}

	private async Task<Exception?> RestoreOnceAsync() {
		await Task.Yield();
		Volatile.Write( ref this.stateValid, 0 );

		List<Exception> exceptions = [];

		try {
			await this.Output.FlushAsync( CancellationToken.None ).ConfigureAwait( false );
		} catch ( Exception exception ) {
			exceptions.Add( exception );
		}

		if ( this.restoreRequired
			&& ( this.baselineMode is not null ) ) {
			try {
				TerminalControlMutationResult result = this.controlProvider.SetMode(
					this.InputEndpoint,
					this.baselineMode,
					GetRestoreTiming( this.baselineMode )
				);

				if ( !result.Succeeded ) {
					exceptions.Add(
						new InvalidOperationException(
							result.Message
							?? "The original terminal input mode could not be restored."
						)
					);
				}
			} catch ( Exception exception ) {
				exceptions.Add( exception );
			}
		}

		this.restoreRequired = false;
		return BuildRestorationException( exceptions );
	}

	private static TerminalModeApplyTiming GetRestoreTiming(
		TerminalModeSnapshot baseline
	) {
		ArgumentNullException.ThrowIfNull( baseline );

		return baseline.Platform switch {
			TerminalPlatformKind.PosixTermios =>
				TerminalModeApplyTiming.AfterOutputDrained,
			TerminalPlatformKind.WindowsConsole =>
				TerminalModeApplyTiming.Immediately,
			_ => throw new ArgumentOutOfRangeException(
				nameof( baseline ),
				baseline.Platform,
				"The terminal platform is not recognized."
			)
		};
	}

	private static Exception? BuildRestorationException(
		IReadOnlyCollection<Exception> exceptions
	) {
		ArgumentNullException.ThrowIfNull( exceptions );

		return exceptions.Count switch {
			0 => null,
			1 => exceptions.First(),
			_ => new AggregateException(
				"Multiple errors occurred while restoring terminal state.",
				exceptions
			)
		};
	}
}
