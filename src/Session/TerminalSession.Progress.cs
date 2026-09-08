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

/// <summary>
/// Terminal-progress ownership integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalProgressManager? progressManager;

	internal TerminalProgressManager ProgressManager {
		get {
			return this.progressManager ??=
				new TerminalProgressManager( this );
		}
	}

	/// <summary>
	/// Acquires one logical terminal-progress owner using OSC 9;4.
	/// </summary>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>A value task containing the acquired progress lease.</returns>
	/// <exception cref="InvalidOperationException">
	/// The output endpoint is not an interactive terminal, the session state is
	/// suspended, or progress cleanup remains unresolved.
	/// </exception>
	/// <exception cref="OperationCanceledException">Acquisition is cancelled.</exception>
	/// <remarks>
	/// Acquisition itself emits no progress frame. The lease begins affecting terminal
	/// progress only after a successful report or indeterminate-state update. Successful
	/// progress emission does not prove that the terminal implements OSC 9;4.
	/// </remarks>
	public async ValueTask<TerminalProgressLease> AcquireProgressAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		TerminalProgressManager manager = this.ProgressManager;
		long ownerId = await manager.AcquireAsync(
			cancellationToken
		).ConfigureAwait( false );
		return new TerminalProgressLease(
			manager,
			ownerId
		);
	}

	private void InvalidateProgressState() {
		this.progressManager?.Invalidate();
	}

	private async ValueTask<Exception?> CloseProgressStateAsync() {
		if ( this.progressManager is null ) {
			return null;
		}

		try {
			await this.progressManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
