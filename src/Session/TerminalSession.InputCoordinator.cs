namespace Icod.Terminal;

/// <summary>
/// Session-owned single-reader input coordination shared by application input
/// and internal terminal query transactions.
/// </summary>
public sealed partial class TerminalSession {
	private readonly object inputCoordinatorSync = new();
	private TerminalInputCoordinator? inputCoordinator;

	internal TerminalInputCoordinator GetInputCoordinator() {
		lock ( this.inputCoordinatorSync ) {
			if ( this.inputCoordinator is not null ) {
				return this.inputCoordinator;
			}

			TerminalInputDecoderOptions decoderOptions = this.Options.InputDecoderOptions;
			TerminalInputDecoder decoder = this.inputDecoder ??= new TerminalInputDecoder(
				this.Input,
				this.Terminal,
				this.Options.MonotonicClock,
				decoderOptions.EscapeSequenceTimeout,
				decoderOptions.MaximumBufferedBytes,
				decoderOptions.PasteChunkBytes
			);
			this.inputCoordinator = new TerminalInputCoordinator(
				decoder,
				this.lifecycleStop.Token
			);
			return this.inputCoordinator;
		}
	}
}
