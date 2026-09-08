/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

using System.Runtime.CompilerServices;

/// <summary>
/// Cross-manager serialization used to preserve screen-local terminal state ordering.
/// </summary>
public sealed partial class TerminalSession {
	private int inputProtocolsReenteredBeforePresentation;

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

	private void ThrowIfStateAcquisitionUnavailable() {
		this.ThrowIfSessionOutputClosed();
		if ( 0 != Volatile.Read( ref this.lifecycleStateReleased ) ) {
			throw new InvalidOperationException(
				"Terminal state cannot be acquired while the session is suspending, suspended, or re-entering terminal state."
			);
		}
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
