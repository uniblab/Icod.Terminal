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

using System.Text;
using Icod.TermInfo;

/// <summary>
/// Serializes all session-owned terminal output while preserving caller ownership
/// of the borrowed <see cref="ITerminalOutput"/> service itself.
/// </summary>
public sealed partial class TerminalSession {
	private readonly SemaphoreSlim controlOutputGate = new( 1, 1 );
	private int acceptingSessionOutput = 1;

	internal async ValueTask<IDisposable> AcquireControlOutputAsync(
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		await this.controlOutputGate.WaitAsync(
			cancellationToken
		).ConfigureAwait( false );
		return new ControlOutputLease( this.controlOutputGate );
	}

	internal async ValueTask<IDisposable> AcquireSessionOutputAsync(
		CancellationToken cancellationToken
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.ThrowIfSessionOutputClosed();

		await this.controlOutputGate.WaitAsync(
			cancellationToken
		).ConfigureAwait( false );
		if ( 0 == Volatile.Read( ref this.acceptingSessionOutput ) ) {
			this.controlOutputGate.Release();
			throw new ObjectDisposedException(
				nameof( TerminalSession ),
				"The terminal session is closing or has already been disposed."
			);
		}

		return new ControlOutputLease( this.controlOutputGate );
	}

	internal void ThrowIfSessionOutputClosed() {
		if ( 0 == Volatile.Read( ref this.acceptingSessionOutput ) ) {
			throw new ObjectDisposedException(
				nameof( TerminalSession ),
				"The terminal session is closing or has already been disposed."
			);
		}
	}

	internal void StopAcceptingSessionOutput() {
		Interlocked.Exchange(
			ref this.acceptingSessionOutput,
			0
		);
	}

	internal ValueTask WriteTerminalStringCoreAsync(
		string value,
		int affectedLines,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( value );
		if ( 0 >= affectedLines ) {
			throw new ArgumentOutOfRangeException(
				nameof( affectedLines ),
				"The number of affected terminal lines must be positive."
			);
		}

		TermInfoOutputOptions outputOptions = new(
			this.Terminal,
			this.outputBaudRate,
			this.Options.CapabilityPaddingMode,
			this.Options.CapabilityDelayProvider
		);

		return TermInfoOutput.TPutsAsync(
			value,
			affectedLines,
			this.terminalOutputStream,
			Encoding.Latin1,
			outputOptions,
			cancellationToken
		);
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
