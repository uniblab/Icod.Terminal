namespace Icod.Terminal;

/// <summary>
/// Provides semantic OSC 7 current-location publication for a live terminal session.
/// </summary>
public sealed partial class TerminalSession {
	/// <summary>
	/// Publishes a filesystem location to the terminal using OSC 7.
	/// </summary>
	/// <param name="path">The native absolute filesystem path to publish.</param>
	/// <param name="pathStyle">The native path grammar used by <paramref name="path"/>.</param>
	/// <param name="authority">
	/// An optional explicit host authority for POSIX or Windows-drive locations.
	/// UNC locations derive their authority from the UNC server name and therefore
	/// do not accept this parameter.
	/// </param>
	/// <param name="cancellationToken">Cancellation observed before transmission begins.</param>
	/// <returns>A value task representing the OSC 7 emission.</returns>
	/// <remarks>
	/// <para>
	/// Publication is explicit. Opening or disposing a session never publishes a
	/// current location automatically, and this method does not read or monitor the
	/// process current directory.
	/// </para>
	/// <para>
	/// The operation is emission-oriented: successful completion means the complete
	/// OSC 7 frame was written to the session output. It does not prove that the
	/// terminal recognized, retained, or used the published location.
	/// </para>
	/// <para>
	/// The path is converted to the canonical <c>file:</c> URI form defined by the
	/// 0.5 contract. The conversion performs no filesystem lookup, existence check,
	/// symlink resolution, or hidden path canonicalization.
	/// </para>
	/// </remarks>
	public async ValueTask PublishCurrentLocationAsync(
		string path,
		TerminalLocationPathStyle pathStyle,
		string? authority = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( path );
		cancellationToken.ThrowIfCancellationRequested();

		TerminalLocationPathKind pathKind = pathStyle switch {
			TerminalLocationPathStyle.Posix => TerminalLocationPathKind.Posix,
			TerminalLocationPathStyle.WindowsDrive => TerminalLocationPathKind.WindowsDrive,
			TerminalLocationPathStyle.WindowsUnc => TerminalLocationPathKind.WindowsUnc,
			_ => throw new ArgumentOutOfRangeException(
				nameof( pathStyle ),
				pathStyle,
				"Unknown terminal location path style."
			)
		};

		if ( !this.OutputObservation.IsTerminal ) {
			throw new InvalidOperationException(
				"OSC 7 current-location publication requires an interactive terminal output endpoint."
			);
		}

		using IDisposable outputLease = await this.AcquireSessionOutputAsync(
			cancellationToken
		).ConfigureAwait( false );
		await OscWriter.WriteLocationAsync(
			this.Output,
			path,
			pathKind,
			authority,
			cancellationToken
		).ConfigureAwait( false );
	}
}
