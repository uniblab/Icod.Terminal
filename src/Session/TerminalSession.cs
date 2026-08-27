namespace Icod.Terminal;

using System.Runtime.ExceptionServices;
using System.Text;
using Icod.TermInfo;

/// <summary>
/// Owns one reversible live-terminal state transition while borrowing the
/// terminal endpoints and byte transports supplied by the caller.
/// </summary>
/// <remarks>
/// A session owns terminal state transitions, not endpoint or stream lifetime.
/// Dispose the session asynchronously to flush pending output, restore output
/// setup, and restore the captured input mode exactly once.
/// </remarks>
public sealed partial class TerminalSession : IAsyncDisposable {
	private readonly object restoreSync = new();
	private readonly ITerminalControlProvider controlProvider;
	private readonly Encoding applicationEncoding;
	private readonly TerminalOutputStream terminalOutputStream;

	private TerminalModeSnapshot? baselineMode;
	private IDisposable? outputModeLease;
	private Task<Exception?>? restoreTask;
	private bool restoreRequired;
	private int? outputBaudRate;
	private int stateValid;

	private TerminalSession(
		ITerminalControlProvider controlProvider,
		TerminalEndpoint inputEndpoint,
		TerminalEndpoint outputEndpoint,
		TerminalEndpointObservation inputObservation,
		TerminalEndpointObservation outputObservation,
		TerminalIdentity identity,
		ITerminalInput input,
		ITerminalOutput output,
		ITerminalLifecycleSource? lifecycleSource,
		TerminalSessionOptions options
	) {
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( inputEndpoint );
		ArgumentNullException.ThrowIfNull( outputEndpoint );
		ArgumentNullException.ThrowIfNull( inputObservation );
		ArgumentNullException.ThrowIfNull( outputObservation );
		ArgumentNullException.ThrowIfNull( identity );
		ArgumentNullException.ThrowIfNull( input );
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( options );

		this.controlProvider = controlProvider;
		this.applicationEncoding = (Encoding)options.ApplicationEncoding.Clone();
		this.terminalOutputStream = new TerminalOutputStream( output );
		this.InputEndpoint = inputEndpoint;
		this.OutputEndpoint = outputEndpoint;
		this.InputObservation = inputObservation;
		this.OutputObservation = outputObservation;
		this.Identity = identity;
		this.Input = input;
		this.Output = output;
		this.lifecycleSource = lifecycleSource;
		this.Options = options;
		this.presentationManager = new TerminalPresentationManager( this );
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

	/// <summary>Gets the terminal identity selected for this session.</summary>
	public TerminalIdentity Identity {
		get;
	}

	/// <summary>Gets the selected immutable terminal capability description.</summary>
	public TerminalDescription Terminal {
		get {
			return this.Identity.Terminal;
		}
	}

	/// <summary>Gets a snapshot of the application-text encoding used by this session.</summary>
	public Encoding ApplicationEncoding {
		get {
			return (Encoding)this.applicationEncoding.Clone();
		}
	}

	/// <summary>Gets the borrowed terminal input byte service.</summary>
	public ITerminalInput Input {
		get;
	}

	/// <summary>Gets the borrowed terminal output byte service.</summary>
	public ITerminalOutput Output {
		get;
	}

	/// <summary>Gets the options object used to open this session.</summary>
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
	/// changes may invalidate state. Managed lifecycle handling invalidates and
	/// re-enters session-owned state automatically; callers may use
	/// <see cref="InvalidateState"/> for other out-of-band changes.
	/// </remarks>
	public bool IsStateValid {
		get {
			return 0 != Volatile.Read( ref this.stateValid );
		}
	}

	/// <summary>
	/// Opens a session against process standard input and standard output.
	/// </summary>
	/// <param name="options">Optional identity, input-mode, output, and encoding policy.</param>
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
	/// <param name="options">Optional identity, input-mode, output, and encoding policy.</param>
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

		TerminalIdentity identity = TerminalIdentityResolver.Resolve(
			resolvedOptions,
			inputObservation,
			outputObservation
		);
		cancellationToken.ThrowIfCancellationRequested();

		ITerminalLifecycleSource? lifecycleSource = ResolveLifecycleSource(
			controlProvider,
			resolvedOptions
		);

		TerminalSession session = new(
			controlProvider,
			inputEndpoint,
			outputEndpoint,
			inputObservation,
			outputObservation,
			identity,
			input,
			output,
			lifecycleSource,
			resolvedOptions
		);

		try {
			await session.InitializeAsync( cancellationToken ).ConfigureAwait( false );
			session.StartLifecyclePump();
			return session;
		} catch ( Exception exception ) {
			await session.StopLifecycleAsync().ConfigureAwait( false );

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
	/// Writes application text using the session's application-text encoding.
	/// </summary>
	/// <param name="value">The application text.</param>
	/// <param name="cancellationToken">Cancellation for the write operation.</param>
	/// <returns>A value task representing the write operation.</returns>
	public ValueTask WriteTextAsync(
		string value,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( value );
		cancellationToken.ThrowIfCancellationRequested();

		byte[] bytes = this.applicationEncoding.GetBytes( value );
		return this.Output.WriteAsync(
			bytes,
			cancellationToken
		);
	}

	/// <summary>
	/// Writes an already-resolved terminfo terminal string with byte-exact
	/// capability encoding and terminal-aware padding semantics.
	/// </summary>
	/// <param name="value">The terminal protocol string.</param>
	/// <param name="affectedLines">The positive number of affected lines for padding.</param>
	/// <param name="cancellationToken">Cancellation for the write operation.</param>
	/// <returns>A value task representing the write operation.</returns>
	public ValueTask WriteTerminalStringAsync(
		string value,
		int affectedLines = 1,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( value );
		if ( 0 >= affectedLines ) {
			throw new ArgumentOutOfRangeException(
				nameof( affectedLines ),
				"The number of affected terminal lines must be positive."
			);
		}

		TermInfoOutputOptions outputOptions = new(
			this.Terminal,
			this.outputBaudRate,
			this.Options.CapabilityPaddingMode,
			this.Options.CapabilityDelayProvider
		);

		return TermInfoOutput.TPutsAsync(
			value,
			affectedLines,
			this.terminalOutputStream,
			Encoding.Latin1,
			outputOptions,
			cancellationToken
		);
	}

	/// <summary>
	/// Writes a non-parameterized string capability when the selected terminal provides it.
	/// </summary>
	/// <param name="capability">The string capability to emit.</param>
	/// <param name="affectedLines">The positive number of affected lines for padding.</param>
	/// <param name="cancellationToken">Cancellation for the write operation.</param>
	/// <returns>
	/// <see langword="true"/> when the capability was present and emitted;
	/// otherwise <see langword="false"/>.
	/// </returns>
	public async ValueTask<bool> WriteCapabilityAsync(
		StringCapability capability,
		int affectedLines = 1,
		CancellationToken cancellationToken = default
	) {
		string? value = this.Terminal.GetString( capability );
		if ( value is null ) {
			return false;
		}

		await this.WriteTerminalStringAsync(
			value,
			affectedLines,
			cancellationToken
		).ConfigureAwait( false );
		return true;
	}

	/// <summary>
	/// Marks the currently applied terminal state invalid after out-of-band host activity.
	/// </summary>
	/// <remarks>
	/// Invalidation does not immediately restore or reapply terminal state. It records
	/// that the session and any active presentation leases can no longer trust their
	/// physical-state assumptions. Lifecycle re-entry re-establishes owned state.
	/// </remarks>
	public void InvalidateState() {
		Volatile.Write( ref this.stateValid, 0 );
		this.InvalidatePresentationState();
	}

	/// <summary>
	/// Flushes pending output and restores session-owned host terminal state exactly once.
	/// </summary>
	/// <returns>A value task representing asynchronous restoration.</returns>
	public async ValueTask DisposeAsync() {
		await this.StopLifecycleAsync().ConfigureAwait( false );

		List<Exception> exceptions = [];
		Exception? presentationException =
			await this.ClosePresentationStateAsync().ConfigureAwait( false );
		if ( presentationException is not null ) {
			exceptions.Add( presentationException );
		}

		Exception? restorationException =
			await this.RestoreCoreAsync().ConfigureAwait( false );
		if ( restorationException is not null ) {
			exceptions.Add( restorationException );
		}

		Exception? exception = BuildRestorationException( exceptions );
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
		this.outputBaudRate = ResolveOutputBaudRate( this.baselineMode );
		cancellationToken.ThrowIfCancellationRequested();

		this.outputModeLease = SystemTerminalOutputSetup.Configure(
			this.controlProvider,
			this.OutputEndpoint,
			this.OutputObservation,
			this.Options.ConfigureOutput
		);
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

		try {
			this.outputModeLease?.Dispose();
		} catch ( Exception exception ) {
			exceptions.Add( exception );
		} finally {
			this.outputModeLease = null;
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

	private static int? ResolveOutputBaudRate(
		TerminalModeSnapshot baseline
	) {
		ArgumentNullException.ThrowIfNull( baseline );

		ulong? baudRate = baseline.OutputSpeed?.BaudRate;
		if ( !baudRate.HasValue
			|| ( 0 == baudRate.Value )
			|| ( int.MaxValue < baudRate.Value ) ) {
			return null;
		}

		return checked( (int)baudRate.Value );
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
