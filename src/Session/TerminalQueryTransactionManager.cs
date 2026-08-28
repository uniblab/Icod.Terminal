namespace Icod.Terminal;

using Icod.Timing;

/// <summary>
/// Serializes ambiguity-sensitive terminal query transactions and preserves
/// bounded ownership after caller cancellation or timeout.
/// </summary>
internal sealed class TerminalQueryTransactionManager {
	internal const int MaximumPendingTransactions = 32;
	internal const int MaximumRequestBytes = 4096;

	internal static TimeSpan MaximumCallerTimeout {
		get;
	} = TimeSpan.FromMinutes( 1 );

	internal static TimeSpan DefaultLateResponseOwnership {
		get;
	} = TimeSpan.FromSeconds( 1 );

	internal static TimeSpan MaximumLateResponseOwnership {
		get;
	} = TimeSpan.FromSeconds( 10 );

	private readonly object sync = new();
	private readonly TerminalSession session;
	private readonly SemaphoreSlim ambiguityGate = new( 1, 1 );
	private readonly CancellationTokenSource stop = new();
	private readonly HashSet<TerminalQueryTransaction> transactions = [];

	private TaskCompletionSource idleCompletion = CreateCompletedCompletion();
	private int pendingCount;
	private long generation;
	private bool suspended;
	private bool closed;

	internal TerminalQueryTransactionManager(
		TerminalSession session
	) {
		ArgumentNullException.ThrowIfNull( session );
		this.session = session;
	}

	internal ValueTask<TerminalResponseFrame> ExecuteAsync(
		ReadOnlyMemory<byte> request,
		ITerminalResponseMatcher matcher,
		TimeSpan timeout,
		TimeSpan lateResponseOwnership,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( matcher );
		TerminalResponseFrameKind frameKind = matcher.FrameKind;
		if ( !Enum.IsDefined( frameKind ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( matcher ),
				frameKind,
				"The terminal response matcher frame kind is not recognized."
			);
		}
		if ( request.IsEmpty ) {
			throw new ArgumentException(
				"A terminal query request cannot be empty.",
				nameof( request )
			);
		}
		if ( MaximumRequestBytes < request.Length ) {
			throw new ArgumentOutOfRangeException(
				nameof( request ),
				request.Length,
				$"A terminal query request cannot exceed {MaximumRequestBytes} bytes."
			);
		}
		if ( TimeSpan.Zero > timeout || MaximumCallerTimeout < timeout ) {
			throw new ArgumentOutOfRangeException(
				nameof( timeout ),
				timeout,
				$"A terminal query timeout must be between zero and {MaximumCallerTimeout}."
			);
		}
		if ( TimeSpan.Zero >= lateResponseOwnership
			|| MaximumLateResponseOwnership < lateResponseOwnership ) {
			throw new ArgumentOutOfRangeException(
				nameof( lateResponseOwnership ),
				lateResponseOwnership,
				"Late-response ownership must be positive and no greater than "
					+ $"{MaximumLateResponseOwnership}."
			);
		}
		cancellationToken.ThrowIfCancellationRequested();

		string? unavailable = this.session.GetQueryUnavailableReason();
		if ( unavailable is not null ) {
			throw new InvalidOperationException( unavailable );
		}

		TerminalQueryTransaction transaction;
		long transactionGeneration;
		lock ( this.sync ) {
			this.ThrowIfClosed();
			if ( this.suspended ) {
				throw new InvalidOperationException(
					"Terminal queries are unavailable while the session is suspended."
				);
			}
			if ( MaximumPendingTransactions <= this.pendingCount ) {
				throw new InvalidOperationException(
					$"The terminal query queue is limited to {MaximumPendingTransactions} transactions."
				);
			}

			transactionGeneration = this.generation;
			transaction = new TerminalQueryTransaction(
				request.ToArray(),
				matcher,
				timeout,
				lateResponseOwnership,
				this.session.Options.MonotonicClock,
				cancellationToken
			);

			if ( 0 == this.pendingCount ) {
				this.idleCompletion = new TaskCompletionSource(
					TaskCreationOptions.RunContinuationsAsynchronously
				);
			}
			++this.pendingCount;
			this.transactions.Add( transaction );
		}

		_ = this.RunTransactionAsync(
			transaction,
			transactionGeneration
		);
		return new ValueTask<TerminalResponseFrame>( transaction.CallerTask );
	}

	internal void Suspend() {
		TerminalQueryTransaction[] pending;
		lock ( this.sync ) {
			if ( this.closed || this.suspended ) {
				return;
			}

			this.suspended = true;
			checked {
				++this.generation;
			}
			pending = this.transactions.ToArray();
		}

		foreach ( TerminalQueryTransaction transaction in pending ) {
			transaction.InterruptCaller(
				new InvalidOperationException(
					"The terminal query was interrupted because the session is suspending."
				)
			);
		}
	}

	internal void Resume() {
		lock ( this.sync ) {
			if ( this.closed ) {
				return;
			}

			this.suspended = false;
		}
	}

	internal async ValueTask CloseAsync() {
		TerminalQueryTransaction[] pending;
		Task idleTask;
		bool cancelStop = false;

		lock ( this.sync ) {
			if ( !this.closed ) {
				this.closed = true;
				this.suspended = true;
				checked {
					++this.generation;
				}
				cancelStop = true;
			}
			pending = this.transactions.ToArray();
			idleTask = this.idleCompletion.Task;
		}

		foreach ( TerminalQueryTransaction transaction in pending ) {
			transaction.InterruptCaller(
				new ObjectDisposedException(
					nameof( TerminalSession ),
					"The terminal session was disposed while a query was outstanding."
				)
			);
		}
		if ( cancelStop ) {
			this.stop.Cancel();
		}

		await idleTask.ConfigureAwait( false );
	}

	private async Task RunTransactionAsync(
		TerminalQueryTransaction transaction,
		long transactionGeneration
	) {
		ArgumentNullException.ThrowIfNull( transaction );

		bool gateHeld = false;
		TerminalInputCoordinator? coordinator = null;
		TerminalResponseExpectation? expectation = null;
		IDisposable? inputDemand = null;

		try {
			using CancellationTokenSource queueWaitCancellation =
				CancellationTokenSource.CreateLinkedTokenSource(
					this.stop.Token,
					transaction.PreEmissionStopToken
				);

			try {
				await this.ambiguityGate.WaitAsync(
					queueWaitCancellation.Token
				).ConfigureAwait( false );
				gateHeld = true;
			} catch ( OperationCanceledException ) when ( transaction.PreEmissionStopToken.IsCancellationRequested
				&& !this.stop.IsCancellationRequested ) {
				return;
			}

			lock ( this.sync ) {
				this.ThrowIfClosed();
				if ( this.suspended || transactionGeneration != this.generation ) {
					transaction.InterruptCaller(
						new InvalidOperationException(
							"The terminal query was invalidated by a session suspend/resume transition."
						)
					);
					return;
				}

			}

			if ( transaction.CallerTask.IsCompleted ) {
				return;
			}

			coordinator = this.session.GetInputCoordinator();
			inputDemand = coordinator.AcquireQueryDemand();
			expectation = coordinator.RegisterResponseExpectation(
				transaction.Matcher,
				armImmediately: false
			);

			Exception? emissionException = null;
			using ( CancellationTokenSource emissionWaitCancellation =
				CancellationTokenSource.CreateLinkedTokenSource(
					this.stop.Token,
					transaction.PreEmissionStopToken
				) ) {
				using IDisposable controlOutput = await this.session.AcquireControlOutputAsync(
					emissionWaitCancellation.Token
				).ConfigureAwait( false );

				if ( !transaction.TryCommitEmission() ) {
					return;
				}

				coordinator.ArmResponseExpectation( expectation );

				try {
					await this.session.Output.WriteAsync(
						transaction.Request,
						this.stop.Token
					).ConfigureAwait( false );
					await this.session.Output.FlushAsync(
						this.stop.Token
					).ConfigureAwait( false );
				} catch ( OperationCanceledException ) when ( this.stop.IsCancellationRequested ) {
					throw;
				} catch ( Exception exception ) {
					emissionException = exception;
					transaction.InterruptCaller( exception );
				}
			}

			await this.WaitForResponseOwnershipAsync(
				transaction,
				coordinator,
				expectation
			).ConfigureAwait( false );
			expectation = null;

			if ( emissionException is not null ) {
				return;
			}
		} catch ( OperationCanceledException ) when ( this.stop.IsCancellationRequested ) {
			transaction.InterruptCaller(
				new ObjectDisposedException(
					nameof( TerminalSession ),
					"The terminal session was disposed while a query was active."
				)
			);
		} catch ( Exception exception ) {
			transaction.InterruptCaller( exception );
		} finally {
			if ( expectation is not null && coordinator is not null ) {
				coordinator.RemoveResponseExpectation( expectation );
			}
			inputDemand?.Dispose();

			if ( gateHeld ) {
				this.ambiguityGate.Release();
			}

			transaction.Dispose();
			this.CompletePendingTransaction( transaction );
		}
	}

	private async ValueTask WaitForResponseOwnershipAsync(
		TerminalQueryTransaction transaction,
		TerminalInputCoordinator coordinator,
		TerminalResponseExpectation expectation
	) {
		ArgumentNullException.ThrowIfNull( transaction );
		ArgumentNullException.ThrowIfNull( coordinator );
		ArgumentNullException.ThrowIfNull( expectation );

		Task<TerminalResponseFrame> responseTask = expectation.Response;
		Task callerTask = transaction.CallerTask;
		TaskCompletionSource stopCompletion = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		using CancellationTokenRegistration stopRegistration = this.stop.Token.Register(
			static state => ( (TaskCompletionSource)state! ).TrySetResult(),
			stopCompletion
		);
		Task stopTask = stopCompletion.Task;

		Task completed = await Task.WhenAny(
			responseTask,
			callerTask,
			stopTask
		).ConfigureAwait( false );
		if ( ReferenceEquals( completed, responseTask ) ) {
			TerminalResponseFrame frame = await responseTask.ConfigureAwait( false );
			transaction.TrySetResponse( frame );
			return;
		}
		if ( ReferenceEquals( completed, stopTask ) ) {
			this.stop.Token.ThrowIfCancellationRequested();
		}

		TimeSpan remainingOwnership = transaction.GetRemainingLateResponseOwnership();
		if ( TimeSpan.Zero >= remainingOwnership ) {
			coordinator.RemoveResponseExpectation( expectation );
			return;
		}

		Task ownershipDelay = this.session.Options.MonotonicClock.DelayAsync(
			remainingOwnership,
			this.stop.Token
		).AsTask();
		completed = await Task.WhenAny(
			responseTask,
			ownershipDelay
		).ConfigureAwait( false );
		if ( ReferenceEquals( completed, responseTask ) ) {
			await responseTask.ConfigureAwait( false );
			return;
		}

		await ownershipDelay.ConfigureAwait( false );
		coordinator.RemoveResponseExpectation( expectation );
	}

	private void CompletePendingTransaction(
		TerminalQueryTransaction transaction
	) {
		ArgumentNullException.ThrowIfNull( transaction );

		TaskCompletionSource? completion = null;
		lock ( this.sync ) {
			this.transactions.Remove( transaction );
			if ( 0 >= this.pendingCount ) {
				return;
			}

			--this.pendingCount;
			if ( 0 == this.pendingCount ) {
				completion = this.idleCompletion;
			}
		}

		completion?.TrySetResult();
	}

	private void ThrowIfClosed() {
		if ( this.closed ) {
			throw new ObjectDisposedException( nameof( TerminalSession ) );
		}
	}

	private static TaskCompletionSource CreateCompletedCompletion() {
		TaskCompletionSource completion = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		completion.TrySetResult();
		return completion;
	}
}

/// <summary>
/// Separates one caller's async lifetime from the lifetime of an emitted wire request.
/// </summary>
internal sealed class TerminalQueryTransaction : IDisposable {
	private readonly object sync = new();
	private readonly IMonotonicClock monotonicClock;
	private readonly TimeSpan lateResponseOwnership;
	private readonly CancellationToken callerCancellationToken;
	private readonly CancellationTokenSource callerLifetimeStop = new();
	private readonly CancellationTokenSource preEmissionStop = new();
	private readonly TaskCompletionSource<TerminalResponseFrame> callerCompletion = new(
		TaskCreationOptions.RunContinuationsAsynchronously
	);
	private readonly CancellationTokenRegistration callerCancellationRegistration;

	private long? callerStoppedTimestamp;
	private bool emitted;
	private int disposed;

	internal TerminalQueryTransaction(
		byte[] request,
		ITerminalResponseMatcher matcher,
		TimeSpan timeout,
		TimeSpan lateResponseOwnership,
		IMonotonicClock monotonicClock,
		CancellationToken callerCancellationToken
	) {
		ArgumentNullException.ThrowIfNull( request );
		if ( 0 == request.Length ) {
			throw new ArgumentException(
				"A terminal query request cannot be empty.",
				nameof( request )
			);
		}
		ArgumentNullException.ThrowIfNull( matcher );
		ArgumentNullException.ThrowIfNull( monotonicClock );
		if ( TimeSpan.Zero > timeout ) {
			throw new ArgumentOutOfRangeException( nameof( timeout ) );
		}
		if ( TimeSpan.Zero >= lateResponseOwnership ) {
			throw new ArgumentOutOfRangeException( nameof( lateResponseOwnership ) );
		}

		this.Request = request.ToArray();
		this.Matcher = matcher;
		this.monotonicClock = monotonicClock;
		this.lateResponseOwnership = lateResponseOwnership;
		this.callerCancellationToken = callerCancellationToken;
		this.callerCancellationRegistration = callerCancellationToken.Register(
			static state => ( (TerminalQueryTransaction)state! ).CancelCaller(),
			this
		);
		_ = this.ObserveTimeoutAsync( timeout );
	}

	internal ReadOnlyMemory<byte> Request {
		get;
	}

	internal ITerminalResponseMatcher Matcher {
		get;
	}

	internal Task<TerminalResponseFrame> CallerTask {
		get {
			return this.callerCompletion.Task;
		}
	}

	internal CancellationToken PreEmissionStopToken {
		get {
			return this.preEmissionStop.Token;
		}
	}

	internal bool TryCommitEmission() {
		lock ( this.sync ) {
			if ( this.callerCompletion.Task.IsCompleted || this.emitted ) {
				return false;
			}

			this.emitted = true;
			return true;
		}
	}

	internal bool TrySetResponse(
		TerminalResponseFrame frame
	) {
		ArgumentNullException.ThrowIfNull( frame );

		lock ( this.sync ) {
			if ( this.callerCompletion.Task.IsCompleted ) {
				return false;
			}

			this.callerLifetimeStop.Cancel();
			return this.callerCompletion.TrySetResult( frame );
		}
	}

	internal void InterruptCaller(
		Exception exception
	) {
		ArgumentNullException.ThrowIfNull( exception );

		lock ( this.sync ) {
			if ( this.callerCompletion.Task.IsCompleted ) {
				return;
			}

			this.callerStoppedTimestamp = this.monotonicClock.GetTimestamp();
			this.callerLifetimeStop.Cancel();
			this.callerCompletion.TrySetException( exception );
			if ( !this.emitted ) {
				this.preEmissionStop.Cancel();
			}
		}
	}

	internal TimeSpan GetRemainingLateResponseOwnership() {
		lock ( this.sync ) {
			if ( !this.callerStoppedTimestamp.HasValue ) {
				return this.lateResponseOwnership;
			}

			TimeSpan elapsed = this.monotonicClock.GetElapsedTime(
				this.callerStoppedTimestamp.Value,
				this.monotonicClock.GetTimestamp()
			);
			return elapsed >= this.lateResponseOwnership
				? TimeSpan.Zero
				: this.lateResponseOwnership - elapsed
			;
		}
	}

	public void Dispose() {
		if ( 0 != Interlocked.Exchange( ref this.disposed, 1 ) ) {
			return;
		}

		this.callerCancellationRegistration.Dispose();
		this.callerLifetimeStop.Cancel();
		this.preEmissionStop.Cancel();
		this.callerLifetimeStop.Dispose();
		this.preEmissionStop.Dispose();
	}

	private void CancelCaller() {
		lock ( this.sync ) {
			if ( this.callerCompletion.Task.IsCompleted ) {
				return;
			}

			this.callerStoppedTimestamp = this.monotonicClock.GetTimestamp();
			this.callerLifetimeStop.Cancel();
			this.callerCompletion.TrySetCanceled( this.callerCancellationToken );
			if ( !this.emitted ) {
				this.preEmissionStop.Cancel();
			}
		}
	}

	private async Task ObserveTimeoutAsync(
		TimeSpan timeout
	) {
		try {
			await this.monotonicClock.DelayAsync(
				timeout,
				this.callerLifetimeStop.Token
			).ConfigureAwait( false );
		} catch ( OperationCanceledException ) when ( this.callerLifetimeStop.IsCancellationRequested ) {
			return;
		}

		lock ( this.sync ) {
			if ( this.callerCompletion.Task.IsCompleted ) {
				return;
			}

			this.callerStoppedTimestamp = this.monotonicClock.GetTimestamp();
			this.callerCompletion.TrySetException(
				new TimeoutException( "The terminal query deadline expired." )
			);
			if ( !this.emitted ) {
				this.preEmissionStop.Cancel();
			}
		}
	}
}
