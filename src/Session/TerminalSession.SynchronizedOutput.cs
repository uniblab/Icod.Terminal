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
/// Synchronized-output ownership integration for <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private TerminalSynchronizedOutputManager? synchronizedOutputManager;

	internal TerminalSynchronizedOutputManager SynchronizedOutputManager {
		get {
			return this.synchronizedOutputManager ??=
				new TerminalSynchronizedOutputManager( this );
		}
	}

	/// <summary>
	/// Acquires one logical synchronized-output request using DEC private mode 2026.
	/// </summary>
	/// <param name="cancellationToken">Cancellation for acquisition only.</param>
	/// <returns>A lease owning one logical synchronized-output request.</returns>
	/// <exception cref="InvalidOperationException">
	/// The output endpoint is not an interactive terminal, terminal state is suspended,
	/// or cleanup remains pending from an earlier failed synchronized-output transition.
	/// </exception>
	/// <exception cref="OperationCanceledException">
	/// The caller cancels acquisition before logical ownership is committed.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The first active lease emits <c>CSI ? 2026 h</c>. Nested logical leases share
	/// the same physical terminal mode and emit no additional begin frame. The final
	/// active lease emits <c>CSI ? 2026 l</c> and flushes once on release.
	/// </para>
	/// <para>
	/// Successful acquisition proves only that the required begin frame was emitted,
	/// or that an existing logical synchronized-output owner was joined. It does not
	/// prove that the terminal recognizes or continues honoring mode 2026.
	/// </para>
	/// </remarks>
	public async ValueTask<TerminalSynchronizedOutputLease> AcquireSynchronizedOutputAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		TerminalSynchronizedOutputManager manager = this.SynchronizedOutputManager;
		long ownerId = await manager.AcquireAsync(
			cancellationToken
		).ConfigureAwait( false );
		return new TerminalSynchronizedOutputLease(
			manager,
			ownerId
		);
	}

	private async ValueTask<Exception?> CloseSynchronizedOutputStateAsync() {
		if ( this.synchronizedOutputManager is null ) {
			return null;
		}

		try {
			await this.synchronizedOutputManager.CloseAsync().ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}
}
