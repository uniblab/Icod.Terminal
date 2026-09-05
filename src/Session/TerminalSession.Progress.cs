namespace Icod.Terminal;

/// <summary>
/// Terminal-progress ownership integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalProgressManager? progressManager;

	internal TerminalProgressManager ProgressManager {
		get {
			return this.progressManager ??=
				new TerminalProgressManager( this );
		}
	}

	private async ValueTask<Exception?> CloseProgressStateAsync() {
		if ( this.progressManager is null ) {
			return null;
		}

		try {
			await this.progressManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
