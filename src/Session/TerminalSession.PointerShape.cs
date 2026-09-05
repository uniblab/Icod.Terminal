namespace Icod.Terminal;

/// <summary>
/// Pointer-shape ownership integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalPointerShapeManager? pointerShapeManager;

	internal TerminalPointerShapeManager PointerShapeManager {
		get {
			return this.pointerShapeManager ??=
				new TerminalPointerShapeManager( this );
		}
	}

	private void InvalidatePointerShapeState() {
		this.pointerShapeManager?.Invalidate();
	}

	private async ValueTask<Exception?> ClosePointerShapeStateAsync() {
		if ( this.pointerShapeManager is null ) {
			return null;
		}

		try {
			await this.pointerShapeManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
