namespace Icod.Terminal;

/// <summary>
/// Cross-manager serialization used to preserve screen-local terminal state ordering.
/// </summary>
public sealed partial class TerminalSession {
	private readonly SemaphoreSlim stateCompositionGate = new( 1, 1 );

	internal TerminalInputProtocolManager InputProtocolManagerForComposition {
		get {
			return this.inputProtocolManager;
		}
	}

	internal async ValueTask<IDisposable> AcquireStateCompositionAsync(
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await this.stateCompositionGate.WaitAsync(
			cancellationToken
		).ConfigureAwait( false );
		return new StateCompositionLease( this.stateCompositionGate );
	}

	private sealed class StateCompositionLease : IDisposable {
		private SemaphoreSlim? gate;

		internal StateCompositionLease(
			SemaphoreSlim gate
		) {
			ArgumentNullException.ThrowIfNull( gate );
			this.gate = gate;
		}

		public void Dispose() {
			SemaphoreSlim? current = Interlocked.Exchange(
				ref this.gate,
				null
			);
			current?.Release();
		}
	}
}
