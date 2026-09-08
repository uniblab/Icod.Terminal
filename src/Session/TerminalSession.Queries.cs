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
/// Internal active-query transaction entry points for the 0.3 protocol families.
/// </summary>
public sealed partial class TerminalSession {
	private readonly object queryTransactionSync = new();
	private TerminalQueryTransactionManager? queryTransactionManager;
	private bool queryTransactionsSuspended;
	private bool queryTransactionsClosed;
	private bool lifecycleObservationQueryWindow;

	internal ValueTask<TerminalResponseFrame> ExecuteQueryAsync(
		ReadOnlyMemory<byte> request,
		ITerminalResponseMatcher matcher,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		return this.ExecuteQueryAsync(
			request,
			matcher,
			timeout,
			TerminalQueryTransactionManager.DefaultLateResponseOwnership,
			cancellationToken
		);
	}

	internal ValueTask<TerminalResponseFrame> ExecuteQueryAsync(
		ReadOnlyMemory<byte> request,
		ITerminalResponseMatcher matcher,
		TimeSpan timeout,
		TimeSpan lateResponseOwnership,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		return this.GetQueryTransactionManager().ExecuteAsync(
			request,
			matcher,
			timeout,
			lateResponseOwnership,
			cancellationToken
		);
	}

	internal ValueTask<TerminalResponseFrame> ExecuteLifecycleObservationQueryAsync(
		ReadOnlyMemory<byte> request,
		ITerminalResponseMatcher matcher,
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		return this.ExecuteLifecycleObservationQueryAsync(
			request,
			matcher,
			timeout,
			TerminalQueryTransactionManager.DefaultLateResponseOwnership,
			cancellationToken
		);
	}

	internal ValueTask<TerminalResponseFrame> ExecuteLifecycleObservationQueryAsync(
		ReadOnlyMemory<byte> request,
		ITerminalResponseMatcher matcher,
		TimeSpan timeout,
		TimeSpan lateResponseOwnership,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( matcher );

		TerminalQueryTransactionManager manager;
		lock ( this.queryTransactionSync ) {
			if ( this.queryTransactionsClosed ) {
				throw new ObjectDisposedException( nameof( TerminalSession ) );
			}
			if ( !this.queryTransactionsSuspended || !this.lifecycleObservationQueryWindow ) {
				throw new InvalidOperationException(
					"Lifecycle observation queries are available only during the internal post-resume observation phase."
				);
			}

			this.queryTransactionManager ??= new TerminalQueryTransactionManager( this );
			manager = this.queryTransactionManager;
		}

		return manager.ExecuteAsync(
			request,
			matcher,
			timeout,
			lateResponseOwnership,
			cancellationToken
		);
	}

	internal void SuspendQueryTransactions() {
		lock ( this.queryTransactionSync ) {
			this.lifecycleObservationQueryWindow = false;
			this.queryTransactionsSuspended = true;
			this.queryTransactionManager?.Suspend();
		}
	}

	internal void BeginLifecycleObservationQueryWindow() {
		lock ( this.queryTransactionSync ) {
			if ( this.queryTransactionsClosed ) {
				throw new ObjectDisposedException( nameof( TerminalSession ) );
			}
			if ( !this.queryTransactionsSuspended ) {
				throw new InvalidOperationException(
					"The lifecycle observation query window requires public query transactions to remain suspended."
				);
			}
			if ( this.lifecycleObservationQueryWindow ) {
				throw new InvalidOperationException(
					"The lifecycle observation query window is already active."
				);
			}

			this.lifecycleObservationQueryWindow = true;
			this.queryTransactionManager?.Resume();
		}
	}

	internal void EndLifecycleObservationQueryWindow() {
		lock ( this.queryTransactionSync ) {
			if ( !this.lifecycleObservationQueryWindow ) {
				return;
			}

			this.lifecycleObservationQueryWindow = false;
			this.queryTransactionManager?.Suspend();
		}
	}

	internal void ResumeQueryTransactions() {
		lock ( this.queryTransactionSync ) {
			if ( this.queryTransactionsClosed ) {
				return;
			}

			this.lifecycleObservationQueryWindow = false;
			this.queryTransactionsSuspended = false;
			this.queryTransactionManager?.Resume();
		}
	}

	internal ValueTask CloseQueryTransactionsAsync() {
		TerminalQueryTransactionManager? manager;
		lock ( this.queryTransactionSync ) {
			this.queryTransactionsClosed = true;
			this.queryTransactionsSuspended = true;
			this.lifecycleObservationQueryWindow = false;
			manager = this.queryTransactionManager;
		}

		return manager is null
			? ValueTask.CompletedTask
			: manager.CloseAsync()
		;
	}

	internal string? GetQueryUnavailableReason() {
		if ( !this.InputObservation.IsTerminal || !this.OutputObservation.IsTerminal ) {
			return "Terminal queries require interactive input and output endpoints.";
		}

		if ( this.InputObservation.Platform != this.OutputObservation.Platform ) {
			return "Terminal query input and output endpoints must use the same terminal platform.";
		}

		if ( TerminalPlatformKind.WindowsConsole == this.InputObservation.Platform ) {
			return null;
		}

		string? inputPath = this.InputObservation.Pathname;
		string? outputPath = this.OutputObservation.Pathname;
		if ( inputPath is null || outputPath is null ) {
			return null;
		}

		return string.Equals(
			inputPath,
			outputPath,
			StringComparison.Ordinal
		)
			? null
			: "Terminal query input and output endpoints identify different terminal devices."
		;
	}

	private TerminalQueryTransactionManager GetQueryTransactionManager() {
		lock ( this.queryTransactionSync ) {
			if ( this.queryTransactionsClosed ) {
				throw new ObjectDisposedException( nameof( TerminalSession ) );
			}
			if ( this.queryTransactionsSuspended ) {
				throw new InvalidOperationException(
					"Terminal queries are unavailable while the session is suspended or resuming."
				);
			}

			this.queryTransactionManager ??= new TerminalQueryTransactionManager( this );
			return this.queryTransactionManager;
		}
	}
}
