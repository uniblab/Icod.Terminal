namespace Icod.Terminal;

/// <summary>
/// Provides semantic DECSCUSR cursor-style mutation for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Sets the terminal text-cursor style using DECSCUSR.
	/// </summary>
	/// <param name="style">The semantic cursor style to emit.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the cursor-style emission.</returns>
	/// <remarks>
	/// <para>
	/// Successful completion means the complete DECSCUSR frame was emitted to the
	/// interactive terminal output endpoint. It does not prove that the terminal
	/// recognized or applied the requested style.
	/// </para>
	/// <para>
	/// Cursor style is independent of cursor visibility. This operation does not
	/// show or hide the cursor and does not acquire any presentation-state lease.
	/// </para>
	/// <para>
	/// Bar styles use the xterm DECSCUSR extension. The library does not infer
	/// support from terminal identity and does not substitute another style when a
	/// terminal ignores an emitted request.
	/// </para>
	/// </remarks>
	public async ValueTask SetCursorStyleAsync(
		TerminalCursorStyle style,
		CancellationToken cancellationToken = default
	) {
		int parameter = TerminalCursorStyleCodec.GetParameter( style );
		cancellationToken.ThrowIfCancellationRequested();

		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"Cursor-style operations require an interactive terminal output endpoint."
			);
		}

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		await CsiWriter.WriteCursorStyleAsync(
			this.Output,
			parameter,
			cancellationToken
		).ConfigureAwait( false );
	}
}
