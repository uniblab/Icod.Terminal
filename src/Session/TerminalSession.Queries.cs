namespace Icod.Terminal;

/// <summary>
/// Internal active-query transaction entry points for the 0.3 protocol families.
/// </summary>
public sealed partial class TerminalSession {
	private readonly object queryTransactionSync = new();
	private TerminalQueryTransactionManager? queryTransactionManager;
	private bool queryTransactionsSuspended;
	private bool queryTransactionsClosed;

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

	internal void SuspendQueryTransactions() {
		lock ( this.queryTransactionSync ) {
			this.queryTransactionsSuspended = true;
			this.queryTransactionManager?.Suspend();
		}
	}

	internal void ResumeQueryTransactions() {
		lock ( this.queryTransactionSync ) {
			if ( this.queryTransactionsClosed ) {
				return;
			}

			this.queryTransactionsSuspended = false;
			this.queryTransactionManager?.Resume();
		}
	}

	internal ValueTask CloseQueryTransactionsAsync() {
		TerminalQueryTransactionManager? manager;
		lock ( this.queryTransactionSync ) {
			this.queryTransactionsClosed = true;
			this.queryTransactionsSuspended = true;
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
					"Terminal queries are unavailable while the session is suspended."
				);
			}

			this.queryTransactionManager ??= new TerminalQueryTransactionManager( this );
			return this.queryTransactionManager;
		}
	}
}
