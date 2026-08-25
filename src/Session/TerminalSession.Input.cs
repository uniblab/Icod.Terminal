namespace Icod.Terminal;

using System.Diagnostics;

/// <summary>
/// Incremental keyboard decoding and unified event-loop input for
/// <see cref="TerminalSession"/>.
/// </summary>
public sealed partial class TerminalSession {
	private const int InputBufferLimit = 4096;

	private static readonly TimeSpan DefaultEscapeDelay =
		TimeSpan.FromMilliseconds( 100 );

	/// <summary>
	/// Gets the default bounded delay used to distinguish an isolated Escape key
	/// from a fragmented escape-prefixed terminfo key sequence.
	/// </summary>
	public static TimeSpan DefaultEscapeSequenceTimeout {
		get {
			return DefaultEscapeDelay;
		}
	}

	/// <summary>
	/// Gets the fixed maximum number of undecoded bytes retained by the 0.1 decoder.
	/// </summary>
	public static int MaximumBufferedInputBytes {
		get {
			return InputBufferLimit;
		}
	}

	private readonly SemaphoreSlim eventReadGate = new( 1, 1 );

	private TerminalInputDecoder? inputDecoder;
	private Task<TerminalInputEvent>? pendingInputEvent;
	private Task<TerminalLifecycleEvent>? pendingInputLifecycleEvent;

	/// <summary>
	/// Waits indefinitely for decoded terminal input or a managed lifecycle event.
	/// </summary>
	/// <remarks>
	/// Caller cancellation is represented by a <see cref="TerminalEventKind.Cancelled"/>
	/// event and does not cancel the underlying terminal read. This preserves bytes
	/// which may form a fragmented UTF-8 scalar or terminal key sequence for the next call.
	/// When lifecycle observation is enabled, this method and
	/// <see cref="ReadLifecycleEventAsync(CancellationToken)"/> consume the same lifecycle queue;
	/// applications should not use both concurrently from independent readers.
	/// </remarks>
	/// <param name="cancellationToken">Cancellation for this wait only.</param>
	/// <returns>The next input, lifecycle, or cancellation event.</returns>
	public ValueTask<TerminalEvent> ReadEventAsync(
		CancellationToken cancellationToken = default
	) {
		return this.ReadEventCoreAsync(
			timeout: null,
			cancellationToken
		);
	}

	/// <summary>
	/// Waits for decoded terminal input or a managed lifecycle event for at most
	/// the supplied interval.
	/// </summary>
	/// <param name="timeout">
	/// A nonnegative timeout, or <see cref="Timeout.InfiniteTimeSpan"/> to wait indefinitely.
	/// </param>
	/// <param name="cancellationToken">Cancellation for this wait only.</param>
	/// <returns>An input, lifecycle, timeout, or cancellation event.</returns>
	public ValueTask<TerminalEvent> ReadEventAsync(
		TimeSpan timeout,
		CancellationToken cancellationToken = default
	) {
		if ( Timeout.InfiniteTimeSpan == timeout ) {
			return this.ReadEventCoreAsync(
				timeout: null,
				cancellationToken
			);
		}
		if ( TimeSpan.Zero > timeout ) {
			throw new ArgumentOutOfRangeException( nameof( timeout ) );
		}

		return this.ReadEventCoreAsync(
			timeout,
			cancellationToken
		);
	}

	/// <summary>
	/// Waits for decoded terminal input or a managed lifecycle event until the
	/// supplied absolute deadline.
	/// </summary>
	/// <param name="deadline">The absolute deadline.</param>
	/// <param name="cancellationToken">Cancellation for this wait only.</param>
	/// <returns>An input, lifecycle, timeout, or cancellation event.</returns>
	public ValueTask<TerminalEvent> ReadEventAsync(
		DateTimeOffset deadline,
		CancellationToken cancellationToken = default
	) {
		TimeSpan remaining = deadline - DateTimeOffset.UtcNow;
		if ( TimeSpan.Zero > remaining ) {
			remaining = TimeSpan.Zero;
		}

		return this.ReadEventAsync(
			remaining,
			cancellationToken
		);
	}

	private async ValueTask<TerminalEvent> ReadEventCoreAsync(
		TimeSpan? timeout,
		CancellationToken cancellationToken
	) {
		if ( cancellationToken.IsCancellationRequested ) {
			return TerminalEvent.Cancelled();
		}

		long waitStarted = Stopwatch.GetTimestamp();

		try {
			if ( timeout.HasValue ) {
				bool entered = await this.eventReadGate.WaitAsync(
					timeout.Value,
					cancellationToken
				).ConfigureAwait( false );
				if ( !entered ) {
					return TerminalEvent.TimedOut();
				}
			} else {
				await this.eventReadGate.WaitAsync( cancellationToken ).ConfigureAwait( false );
			}
		} catch ( OperationCanceledException ) when ( cancellationToken.IsCancellationRequested ) {
			return TerminalEvent.Cancelled();
		}

		if ( timeout.HasValue ) {
			TimeSpan elapsed = Stopwatch.GetElapsedTime( waitStarted );
			timeout = elapsed >= timeout.Value
				? TimeSpan.Zero
				: timeout.Value - elapsed
			;
		}

		try {
			Task<TerminalInputEvent> inputTask = this.GetPendingInputEvent();
			Task<TerminalLifecycleEvent>? lifecycleTask = this.GetPendingInputLifecycleEvent();

			if ( lifecycleTask is not null && lifecycleTask.IsCompleted ) {
				return TerminalEvent.FromLifecycle(
					await this.CompleteInputLifecycleEventAsync(
						lifecycleTask
					).ConfigureAwait( false )
				);
			}
			if ( inputTask.IsCompleted ) {
				return TerminalEvent.FromInput(
					await this.CompleteInputEventAsync(
						inputTask
					).ConfigureAwait( false )
				);
			}
			if ( TimeSpan.Zero == timeout ) {
				return TerminalEvent.TimedOut();
			}

			Task waitTask = Task.Delay(
				timeout ?? Timeout.InfiniteTimeSpan,
				cancellationToken
			);
			Task completed = lifecycleTask is null
				? await Task.WhenAny(
					inputTask,
					waitTask
				).ConfigureAwait( false )
				: await Task.WhenAny(
					inputTask,
					lifecycleTask,
					waitTask
				).ConfigureAwait( false )
			;

			if ( lifecycleTask is not null
				&& ReferenceEquals( completed, lifecycleTask ) ) {
				return TerminalEvent.FromLifecycle(
					await this.CompleteInputLifecycleEventAsync(
						lifecycleTask
					).ConfigureAwait( false )
				);
			}
			if ( ReferenceEquals( completed, inputTask ) ) {
				return TerminalEvent.FromInput(
					await this.CompleteInputEventAsync(
						inputTask
					).ConfigureAwait( false )
				);
			}
			if ( cancellationToken.IsCancellationRequested ) {
				return TerminalEvent.Cancelled();
			}

			return TerminalEvent.TimedOut();
		} finally {
			this.eventReadGate.Release();
		}
	}

	private Task<TerminalInputEvent> GetPendingInputEvent() {
		TerminalInputDecoder decoder = this.inputDecoder ??= new TerminalInputDecoder(
			this.Input,
			this.Terminal,
			DefaultEscapeDelay,
			MaximumBufferedInputBytes
		);
		this.pendingInputEvent ??= decoder.ReadAsync(
			this.lifecycleStop.Token
		).AsTask();

		return this.pendingInputEvent;
	}

	private Task<TerminalLifecycleEvent>? GetPendingInputLifecycleEvent() {
		if ( !this.SupportsLifecycleEvents ) {
			return null;
		}

		this.pendingInputLifecycleEvent ??= this.lifecycleEvents.Reader.ReadAsync(
			this.lifecycleStop.Token
		).AsTask();
		return this.pendingInputLifecycleEvent;
	}

	private async ValueTask<TerminalInputEvent> CompleteInputEventAsync(
		Task<TerminalInputEvent> inputTask
	) {
		ArgumentNullException.ThrowIfNull( inputTask );

		try {
			return await inputTask.ConfigureAwait( false );
		} finally {
			if ( ReferenceEquals( this.pendingInputEvent, inputTask ) ) {
				this.pendingInputEvent = null;
			}
		}
	}

	private async ValueTask<TerminalLifecycleEvent> CompleteInputLifecycleEventAsync(
		Task<TerminalLifecycleEvent> lifecycleTask
	) {
		ArgumentNullException.ThrowIfNull( lifecycleTask );

		try {
			return await lifecycleTask.ConfigureAwait( false );
		} finally {
			if ( ReferenceEquals( this.pendingInputLifecycleEvent, lifecycleTask ) ) {
				this.pendingInputLifecycleEvent = null;
			}
		}
	}
}
