namespace Icod.Terminal;

/// <summary>
/// OSC 133 semantic-prompt marker integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
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
