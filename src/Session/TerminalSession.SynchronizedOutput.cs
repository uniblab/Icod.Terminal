namespace Icod.Terminal;

/// <summary>
/// Internal synchronized-output ownership integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalSynchronizedOutputManager? synchronizedOutputManager;

	internal TerminalSynchronizedOutputManager SynchronizedOutputManager {
		get {
			return this.synchronizedOutputManager ??=
				new TerminalSynchronizedOutputManager( this );
		}
	}

	private async ValueTask<Exception?> CloseSynchronizedOutputStateAsync() {
		if ( this.synchronizedOutputManager is null ) {
			return null;
		}

		try {
			await this.synchronizedOutputManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
