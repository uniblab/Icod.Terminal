namespace Icod.Terminal;

/// <summary>
/// Owns one session-managed terminal cursor-style request.
/// </summary>
/// <remarks>
/// Cursor-style leases are strictly nested. Releasing an inner lease restores the
/// immediately preceding session-owned cursor style. Releasing the outermost lease
/// restores the cursor style which was explicitly observed before the outer lease
/// was acquired.
/// </remarks>
public sealed class TerminalCursorStyleLease : IAsyncDisposable {
	private readonly object sync = new();
	private readonly long leaseId;
	private TerminalCursorStyleManager? owner;
	private Task? disposeTask;

	internal TerminalCursorStyleLease(
		TerminalCursorStyleManager owner,
		long leaseId,
		TerminalCursorStyle style
	) {
		ArgumentNullException.ThrowIfNull( owner );
		if ( !Enum.IsDefined( style ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( style ),
				style,
				"The terminal cursor style is not recognized."
			);
		}

		this.owner = owner;
		this.leaseId = leaseId;
		this.Style = style;
	}

	/// <summary>Gets the cursor style owned by this lease.</summary>
	public TerminalCursorStyle Style {
		get;
	}

	/// <summary>
	/// Releases this cursor-style request and restores the immediately preceding
	/// session-owned style or the observed pre-lease terminal style.
	/// </summary>
	/// <returns>A value task representing asynchronous restoration.</returns>
	public async ValueTask DisposeAsync() {
		Task? task;
		lock ( this.sync ) {
			if ( this.owner is null ) {
				return;
			}

			this.disposeTask ??= this.DisposeCoreAsync();
			task = this.disposeTask;
		}

		try {
			await task.ConfigureAwait( false );
		} finally {
			lock ( this.sync ) {
				if ( ReferenceEquals( this.disposeTask, task ) ) {
					this.disposeTask = null;
				}
			}
		}
	}

	internal void MarkReleasedByOwner() {
		lock ( this.sync ) {
			this.owner = null;
			this.disposeTask = null;
		}
	}

	private async Task DisposeCoreAsync() {
		TerminalCursorStyleManager? currentOwner;
		lock ( this.sync ) {
			currentOwner = this.owner;
		}
		if ( currentOwner is null ) {
			return;
		}

		await currentOwner.ReleaseAsync( this.leaseId ).ConfigureAwait( false );
		lock ( this.sync ) {
			this.owner = null;
		}
	}
}
