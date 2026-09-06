namespace Icod.Terminal;

/// <summary>
/// OSC 133 semantic-prompt marker integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Emits the portable OSC 133 semantic marker indicating that a prompt begins.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// This operation is independently callable and does not require the session to track a
	/// command-region state machine. Successful completion proves only that the complete marker
	/// frame was written; it does not prove terminal support. The operation does not flush.
	/// </remarks>
	public ValueTask BeginPromptAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreatePromptStart(),
			cancellationToken
		);
	}

	/// <summary>
	/// Emits an OSC 133 prompt-start marker with typed extended semantic metadata.
	/// </summary>
	/// <param name="options">Validated prompt metadata. The default value is equivalent to the portable bare prompt marker.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="ArgumentOutOfRangeException">One of the option enum values is undefined.</exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// The operation emits only explicitly represented metadata in the canonical 0.15 parameter order.
	/// It does not probe terminal support, configure shell bindings, or enable a mouse protocol.
	/// </remarks>
	public ValueTask BeginPromptAsync(
		TerminalSemanticPromptOptions options,
		CancellationToken cancellationToken = default
	) {
		options.Validate();
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteExtendedSemanticPromptAsync(
			options,
			cancellationToken
		);
	}

	/// <summary>
	/// Emits the portable OSC 133 semantic marker indicating that command input begins.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// This marker also denotes the end of the prompt. It is independently callable and does
	/// not require a preceding prompt-start marker through this session. The operation does not flush.
	/// </remarks>
	public ValueTask BeginCommandInputAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandInputStart(),
			cancellationToken
		);
	}

	/// <summary>
	/// Emits the portable OSC 133 semantic marker indicating that command output begins.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// This marker denotes that command execution/output has begun. It is independently callable
	/// and does not require preceding prompt or input markers through this session. The operation does not flush.
	/// </remarks>
	public ValueTask BeginCommandOutputAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandOutputStart(),
			cancellationToken
		);
	}

	/// <summary>
	/// Emits an OSC 133 command-output marker with optional typed command-line metadata.
	/// </summary>
	/// <param name="options">
	/// Command-output metadata. The default value is equivalent to the portable bare command-output marker.
	/// A non-null command line is encoded as <c>cmdline_url</c>.
	/// </param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="ArgumentException">
	/// The supplied command line is ill-formed Unicode or its encoded OSC 133 payload exceeds the 0.15 safety bound.
	/// </exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// Command-line publication is explicit caller intent. The library does not inspect shell history,
	/// capture process command lines, parse shell syntax, infer sensitivity, or redact secrets. Percent
	/// encoding protects OSC framing only and does not provide confidentiality.
	/// </remarks>
	public ValueTask BeginCommandOutputAsync(
		TerminalSemanticCommandOutputOptions options,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( options.CommandLine is null ) {
			return this.WriteSemanticPromptMarkerAsync(
				TerminalSemanticPromptMarker.CreateCommandOutputStart(),
				cancellationToken
			);
		}

		return this.WriteExtendedSemanticCommandOutputAsync(
			options.CommandLine,
			cancellationToken
		);
	}

	/// <summary>
	/// Emits the portable OSC 133 semantic marker indicating that a command finished with an exit status.
	/// </summary>
	/// <param name="exitStatus">The command exit status in the portable OSC 133 range 0 through 255.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// Status zero is a completed successful command and is semantically distinct from
	/// <see cref="AbortCommandAsync(CancellationToken)"/>, which emits a bare completion marker with no status.
	/// This operation is independently callable and does not flush.
	/// </remarks>
	public ValueTask FinishCommandAsync(
		byte exitStatus,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandFinished( exitStatus ),
			cancellationToken
		);
	}

	/// <summary>
	/// Emits the portable OSC 133 bare completion marker for an aborted or cancelled command region.
	/// </summary>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing marker emission.</returns>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// Abort carries no exit status and is not an alias for successful completion with status zero.
	/// This operation is independently callable and does not flush.
	/// </remarks>
	public ValueTask AbortCommandAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteSemanticPromptMarkerAsync(
			TerminalSemanticPromptMarker.CreateCommandAborted(),
			cancellationToken
		);
	}

	internal async ValueTask WriteSemanticPromptMarkerAsync(
		TerminalSemanticPromptMarker marker,
		CancellationToken cancellationToken = default
	) {
		TerminalSemanticPromptMarkerCodec.Validate( marker );
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateSemanticPromptOutputEndpoint();

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		await TerminalSemanticPromptMarkerCodec.WriteAsync(
			this.Output,
			marker,
			cancellationToken
		).ConfigureAwait( false );
	}

	private async ValueTask WriteExtendedSemanticPromptAsync(
		TerminalSemanticPromptOptions options,
		CancellationToken cancellationToken
	) {
		options.Validate();
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateSemanticPromptOutputEndpoint();

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		await OscWriter.WriteOsc133PromptStartAsync(
			this.Output,
			TerminalSemanticPromptResizeBehavior.ShellDoesNotRedrawPrompt == options.ResizeBehavior,
			options.UseSpecialCursorKey,
			TerminalSemanticPromptKind.Secondary == options.Kind,
			(byte)options.ClickMode,
			cancellationToken
		).ConfigureAwait( false );
	}

	private async ValueTask WriteExtendedSemanticCommandOutputAsync(
		string commandLine,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( commandLine );
		cancellationToken.ThrowIfCancellationRequested();
		this.ValidateSemanticPromptOutputEndpoint();

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		await OscWriter.WriteOsc133CommandOutputStartAsync(
			this.Output,
			commandLine,
			cancellationToken
		).ConfigureAwait( false );
	}

	private void ValidateSemanticPromptOutputEndpoint() {
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 133 semantic prompt markers require an interactive terminal output endpoint."
			);
		}
	}
}
