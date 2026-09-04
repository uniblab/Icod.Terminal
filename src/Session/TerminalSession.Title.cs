namespace Icod.Terminal;

/// <summary>
/// Provides semantic OSC title operations for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Sets both the terminal icon name and window title using OSC 0.
	/// </summary>
	/// <param name="value">The title text to emit.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the title emission.</returns>
	/// <remarks>
	/// The operation is emission-oriented: successful completion means the complete
	/// OSC 0 frame was written to the session output. It does not prove that the
	/// terminal applied the title. The title text is validated by the internal OSC
	/// writer before any output occurs.
	/// </remarks>
	public ValueTask SetTitleAsync(
		string value,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( value );
		cancellationToken.ThrowIfCancellationRequested();

		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC title operations require an interactive terminal output endpoint."
			);
		}

		return OscWriter.WriteTitleAsync(
			this.Output,
			OscTitleSelector.IconAndWindowTitle,
			value,
			cancellationToken
		);
	}
}
