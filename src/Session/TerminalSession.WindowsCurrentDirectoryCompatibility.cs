namespace Icod.Terminal;

/// <summary>
/// Provides explicit OSC 9;9 Windows-current-directory compatibility publication.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Publishes one caller-supplied Windows filesystem path using the OSC 9;9 compatibility form.
	/// </summary>
	/// <param name="windowsPath">The Windows filesystem path to publish.</param>
	/// <param name="cancellationToken">Cancellation observed before transmission is committed.</param>
	/// <returns>A value task representing compatibility-hint emission.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="windowsPath"/> is <see langword="null"/>.</exception>
	/// <exception cref="ArgumentException">
	/// The path is empty, contains malformed Unicode or C0/C1/DEL controls, or exceeds the 0.16 OSC 9;9 payload bound.
	/// </exception>
	/// <exception cref="InvalidOperationException">The output endpoint is not an interactive terminal.</exception>
	/// <exception cref="ObjectDisposedException">The terminal session is closing or has been disposed.</exception>
	/// <exception cref="OperationCanceledException">The caller cancels before transmission is committed.</exception>
	/// <remarks>
	/// This is an explicitly secondary Windows Terminal/ConEmu compatibility mechanism. The portable
	/// <see cref="PublishCurrentLocationAsync(string, TerminalLocationPathStyle, string?, CancellationToken)"/>
	/// OSC 7 API remains the preferred current-location protocol. This method never translates a POSIX path,
	/// inspects the process current directory, probes terminal identity, or emits OSC 7 automatically.
	/// </remarks>
	public ValueTask PublishWindowsCurrentDirectoryCompatibilityAsync(
		string windowsPath,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( windowsPath );
		cancellationToken.ThrowIfCancellationRequested();
		return this.WriteWindowsCurrentDirectoryCompatibilityAsync(
			windowsPath,
			cancellationToken
		);
	}

	private async ValueTask WriteWindowsCurrentDirectoryCompatibilityAsync(
		string windowsPath,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( windowsPath );
		cancellationToken.ThrowIfCancellationRequested();
		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 9;9 current-directory compatibility publication requires an interactive terminal output endpoint."
			);
		}

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		cancellationToken.ThrowIfCancellationRequested();

		await OscWriter.WriteOsc9WindowsCurrentDirectoryAsync(
			this.Output,
			windowsPath,
			cancellationToken
		).ConfigureAwait( false );
	}
}
