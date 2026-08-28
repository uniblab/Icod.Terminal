namespace Icod.Terminal;

/// <summary>
/// Serializes session-owned terminal control output without serializing
/// ordinary application output.
/// </summary>
public sealed partial class TerminalSession {
	private readonly SemaphoreSlim controlOutputGate = new( 1, 1 );

	internal async ValueTask<IDisposable> AcquireControlOutputAsync(
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await this.controlOutputGate.WaitAsync(
			cancellationToken
		).ConfigureAwait( false );
		return new ControlOutputLease( this.controlOutputGate );
	}

	private sealed class ControlOutputLease : IDisposable {
		private SemaphoreSlim? gate;

		internal ControlOutputLease(
			SemaphoreSlim gate
		) {
			ArgumentNullException.ThrowIfNull( gate );
			this.gate = gate;
		}

		public void Dispose() {
			SemaphoreSlim? prior = Interlocked.Exchange(
				ref this.gate,
				null
			);
			prior?.Release();
		}
	}
}
