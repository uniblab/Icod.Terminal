namespace Icod.Terminal;

/// <summary>
/// Cross-manager access used to preserve screen-local terminal state ordering.
/// </summary>
public sealed partial class TerminalSession {
	internal TerminalInputProtocolManager InputProtocolManagerForComposition {
		get {
			return this.inputProtocolManager;
		}
	}
}
