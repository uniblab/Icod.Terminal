namespace Icod.Terminal;

using System.Runtime.CompilerServices;

/// <summary>
/// Cross-manager serialization used to preserve screen-local terminal state ordering.
/// </summary>
public sealed partial class TerminalSession {
	internal TerminalInputProtocolManager InputProtocolManagerForComposition {
		get {
			return this.inputProtocolManager;
		}
	}

	internal ValueTask<IDisposable> AcquireStateCompositionAsync(
		CancellationToken cancellationToken
	) {
		return TerminalStateComposition.AcquireAsync(
			this.inputProtocolManager,
			this.presentationManager,
			cancellationToken
		);
	}
}

/// <summary>
/// Associates both state managers for one session with one weakly-held composition gate.
/// </summary>
internal static class TerminalStateComposition {
	private static readonly object Sync = new();
	private static readonly ConditionalWeakTable<TerminalInputProtocolManager, GateHolder>
		InputGates = new();
	private static readonly ConditionalWeakTable<TerminalPresentationManager, GateHolder>
		PresentationGates = new();

	internal static async ValueTask<IDisposable> AcquireAsync(
		TerminalInputProtocolManager inputManager,
		TerminalPresentationManager presentationManager,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( inputManager );
		ArgumentNullException.ThrowIfNull( presentationManager );
		cancellationToken.ThrowIfCancellationRequested();

		SemaphoreSlim gate = GetOrRegisterGate(
			inputManager,
			presentationManager
		);
		await gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		return new GateLease( gate );
	}

	internal static async ValueTask<IDisposable> AcquireAsync(
		TerminalInputProtocolManager inputManager,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( inputManager );
		cancellationToken.ThrowIfCancellationRequested();

		SemaphoreSlim gate = GetRegisteredGate( inputManager );
		await gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		return new GateLease( gate );
	}

	internal static async ValueTask<IDisposable> AcquireAsync(
		TerminalPresentationManager presentationManager,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( presentationManager );
		cancellationToken.ThrowIfCancellationRequested();

		SemaphoreSlim gate = GetRegisteredGate( presentationManager );
		await gate.WaitAsync( cancellationToken ).ConfigureAwait( false );
		return new GateLease( gate );
	}

	private static SemaphoreSlim GetOrRegisterGate(
		TerminalInputProtocolManager inputManager,
		TerminalPresentationManager presentationManager
	) {
		lock ( Sync ) {
			if ( InputGates.TryGetValue( inputManager, out GateHolder? inputHolder ) ) {
				if ( !PresentationGates.TryGetValue( presentationManager, out _ ) ) {
					PresentationGates.Add( presentationManager, inputHolder );
				}
				return inputHolder.Gate;
			}

			if ( PresentationGates.TryGetValue(
				presentationManager,
				out GateHolder? presentationHolder
			) ) {
				InputGates.Add( inputManager, presentationHolder );
				return presentationHolder.Gate;
			}

			GateHolder holder = new();
			InputGates.Add( inputManager, holder );
			PresentationGates.Add( presentationManager, holder );
			return holder.Gate;
		}
	}

	private static SemaphoreSlim GetRegisteredGate(
		TerminalInputProtocolManager inputManager
	) {
		lock ( Sync ) {
			if ( InputGates.TryGetValue( inputManager, out GateHolder? holder ) ) {
				return holder.Gate;
			}
		}

		throw new InvalidOperationException(
			"The terminal input-protocol manager is not registered for state composition."
		);
	}

	private static SemaphoreSlim GetRegisteredGate(
		TerminalPresentationManager presentationManager
	) {
		lock ( Sync ) {
			if ( PresentationGates.TryGetValue(
				presentationManager,
				out GateHolder? holder
			) ) {
				return holder.Gate;
			}
		}

		throw new InvalidOperationException(
			"The terminal presentation manager is not registered for state composition."
		);
	}

	private sealed class GateHolder {
		internal SemaphoreSlim Gate {
			get;
		} = new( 1, 1 );
	}

	private sealed class GateLease : IDisposable {
		private SemaphoreSlim? gate;

		internal GateLease(
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
