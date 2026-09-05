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

	private void ValidateSemanticPromptOutputEndpoint() {
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 133 semantic prompt markers require an interactive terminal output endpoint."
			);
		}
	}
}
