namespace Icod.Terminal;

/// <summary>
/// Cross-manager serialization for terminal state that must compose on one physical screen.
/// </summary>
public sealed partial class TerminalSession {
	private readonly SemaphoreSlim stateCompositionGate = new( 1, 1 );

	internal SemaphoreSlim StateCompositionGate {
		get {
			return this.stateCompositionGate;
		}
	}

	internal TerminalInputProtocolManager InputProtocolManagerForComposition {
		get {
			return this.inputProtocolManager;
		}
	}
}
