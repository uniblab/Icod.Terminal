/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal.Tests.Screen;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>Hardens the bounded T176 screen-output transaction foundation.</summary>
public sealed class TerminalScreenOutputTransactionHardeningTests {
	private const int MaximumItemCount = 65_536;

	[Fact]
	public async Task TextItemCountAcceptsBoundaryAndRejectsBoundaryPlusOne() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		for ( int count = 0; count < MaximumItemCount; ++count ) {
			transaction.WriteText( "x" );
		}

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => transaction.WriteText( "x" )
		);
		await transaction.CommitAsync();

		Assert.Equal(
			"The screen-output transaction exceeds its item-count limit.",
			exception.Message
		);
		Assert.Equal(
			Encoding.UTF8.GetBytes( new string( 'x', MaximumItemCount ) ),
			output.GetCombinedWrites()
		);
		Assert.Equal( 1, output.FlushAttemptCount );
	}

	[Fact]
	public async Task PlanItemCountAcceptsBoundaryAndRejectsBoundaryPlusOne() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOperationPlan plan = session.Screen.PlanAlert(
			TerminalAlertKind.Audible
		) ?? throw new InvalidOperationException();
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		for ( int count = 0; count < MaximumItemCount; ++count ) {
			transaction.Add( plan );
		}

		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => transaction.Add( plan )
		);
		await transaction.CommitAsync();

		Assert.Equal(
			"The screen-output transaction exceeds its item-count limit.",
			exception.Message
		);
		Assert.Equal( MaximumItemCount, output.WriteAttemptCount );
		Assert.Equal( "<bell>"u8.ToArray(), output.GetWrite( 0 ) );
		Assert.Equal( "<bell>"u8.ToArray(), output.GetWrite( MaximumItemCount - 1 ) );
		Assert.Equal( 1, output.FlushAttemptCount );
	}

	[Fact]
	public async Task DisposedSessionRejectsTransactionMutation() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOperationPlan plan = session.Screen.PlanAlert(
			TerminalAlertKind.Audible
		) ?? throw new InvalidOperationException();
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		await session.DisposeAsync();

		ObjectDisposedException textException = Assert.Throws<ObjectDisposedException>(
			() => transaction.WriteText( "after-dispose" )
		);
		ObjectDisposedException planException = Assert.Throws<ObjectDisposedException>(
			() => transaction.Add( plan )
		);

		Assert.Equal( nameof( TerminalSession ), textException.ObjectName );
		Assert.Equal( textException.ObjectName, planException.ObjectName );
	}

	[Fact]
	public async Task CommittedTransactionRetainsMutationPrecedenceAfterSessionDisposal() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOperationPlan plan = session.Screen.PlanAlert(
			TerminalAlertKind.Audible
		) ?? throw new InvalidOperationException();
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		await transaction.CommitAsync();
		await session.DisposeAsync();

		InvalidOperationException textException = Assert.Throws<InvalidOperationException>(
			() => transaction.WriteText( "late" )
		);
		InvalidOperationException planException = Assert.Throws<InvalidOperationException>(
			() => transaction.Add( plan )
		);

		Assert.Equal(
			"A screen-output transaction cannot be changed after commit begins.",
			textException.Message
		);
		Assert.Equal( textException.Message, planException.Message );
	}

	[Fact]
	public async Task TransactionRejectsDefaultAndForeignPlansWithoutMutation() {
		RecordingTerminalOutput firstOutput = new();
		RecordingTerminalOutput secondOutput = new();
		await using TerminalSession first = await OpenSessionAsync( firstOutput );
		await using TerminalSession second = await OpenSessionAsync( secondOutput );
		TerminalScreenOperationPlan foreign = first.Screen.PlanAlert(
			TerminalAlertKind.Audible
		) ?? throw new InvalidOperationException();
		TerminalScreenOutputTransaction transaction =
			second.CreateScreenOutputTransaction();

		ArgumentException defaultException = Assert.Throws<ArgumentException>(
			() => transaction.Add( default )
		);
		ArgumentException foreignException = Assert.Throws<ArgumentException>(
			() => transaction.Add( foreign )
		);
		transaction.WriteText( "owned" );
		await transaction.CommitAsync();

		Assert.Equal( "plan", defaultException.ParamName );
		Assert.Equal( "The default screen-operation plan is not valid. (Parameter 'plan')", defaultException.Message );
		Assert.Equal( "plan", foreignException.ParamName );
		Assert.Equal(
			"The screen-operation plan belongs to another terminal session. (Parameter 'plan')",
			foreignException.Message
		);
		Assert.Equal( "owned"u8.ToArray(), secondOutput.GetCombinedWrites() );
	}

	[Fact]
	public async Task Utf8PayloadBoundaryRejectionDoesNotPoisonLaterAdds() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		const int maximumPayloadByteCount = 64 * 1024 * 1024;
		string threeByteCharacters = new( '\u0800', maximumPayloadByteCount / 3 );

		transaction.WriteText( threeByteCharacters );
		transaction.WriteText( "a" );
		InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
			() => transaction.WriteText( "b" )
		);
		transaction.WriteText( string.Empty );

		Assert.Equal(
			"The screen-output transaction exceeds its application-payload limit.",
			exception.Message
		);
	}

	[Fact]
	public async Task EmptyCommitFlushesAndAdvancesTheOutputEpoch() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction stale =
			session.CreateScreenOutputTransaction();
		TerminalScreenOutputTransaction empty =
			session.CreateScreenOutputTransaction();

		await empty.CommitAsync();
		InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
			() => stale.CommitAsync().AsTask()
		);

		Assert.Equal(
			"The screen-output transaction is stale because intervening session output occurred.",
			exception.Message
		);
		Assert.Equal( 0, output.WriteAttemptCount );
		Assert.Equal( 1, output.FlushAttemptCount );
	}

	[Fact]
	public async Task PreCancelledCommitIsConsumedWithoutOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.WriteText( "cancelled" );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => transaction.CommitAsync( cancellation.Token ).AsTask()
		);
		InvalidOperationException second = await Assert.ThrowsAsync<InvalidOperationException>(
			() => transaction.CommitAsync().AsTask()
		);

		Assert.Equal(
			"A screen-output transaction can be committed only once.",
			second.Message
		);
		Assert.Equal( 0, output.WriteAttemptCount );
		Assert.Equal( 0, output.FlushAttemptCount );
	}

	[Fact]
	public async Task CommittedTransactionSerializesOrdinarySessionOutput() {
		RecordingTerminalOutput output = new() {
			PauseAfterWriteAttempt = 1
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.WriteText( "transaction" );

		Task commit = transaction.CommitAsync().AsTask();
		await output.WaitUntilPausedAsync().WaitAsync( TimeSpan.FromSeconds( 5 ) );
		Task ordinary = session.WriteTextAsync( "ordinary" ).AsTask();

		try {
			Assert.False( ordinary.IsCompleted );
		} finally {
			output.ReleasePause();
			await Task.WhenAll( commit, ordinary ).WaitAsync( TimeSpan.FromSeconds( 5 ) );
		}

		Assert.Equal(
			"transactionordinary"u8.ToArray(),
			output.GetCombinedWrites()
		);
		Assert.Equal( 1, output.FlushAttemptCount );
	}

	[Fact]
	public async Task SameEpochTransactionsSerializeAndOnlyFirstCommitEmits() {
		RecordingTerminalOutput output = new() {
			PauseAfterWriteAttempt = 1
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction first =
			session.CreateScreenOutputTransaction();
		first.WriteText( "first" );
		TerminalScreenOutputTransaction second =
			session.CreateScreenOutputTransaction();
		second.WriteText( "second" );

		Task firstCommit = first.CommitAsync().AsTask();
		await output.WaitUntilPausedAsync().WaitAsync( TimeSpan.FromSeconds( 5 ) );
		Task secondCommit = second.CommitAsync().AsTask();
		Exception? secondFailure = null;
		try {
			Assert.False( secondCommit.IsCompleted );
		} finally {
			output.ReleasePause();
			try {
				await firstCommit.WaitAsync( TimeSpan.FromSeconds( 5 ) );
			} finally {
				try {
					await secondCommit.WaitAsync( TimeSpan.FromSeconds( 5 ) );
				} catch ( Exception caughtFailure ) {
					secondFailure = caughtFailure;
				}
			}
		}
		InvalidOperationException exception = Assert.IsType<InvalidOperationException>(
			secondFailure
		);

		Assert.Equal(
			"The screen-output transaction is stale because intervening session output occurred.",
			exception.Message
		);
		Assert.Equal( "first"u8.ToArray(), output.GetCombinedWrites() );
		Assert.Equal( 1, output.FlushAttemptCount );
	}

	[Fact]
	public async Task CancelledGateWaitReleasesSynchronizedReservationWithoutOutput() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		IDisposable blockedOutput = await session.AcquireSessionOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();
		using CancellationTokenSource ownerCancellation = new();
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "cancelled" );

		Task commit = transaction.CommitAsync( cancellation.Token ).AsTask();
		Task<TerminalSynchronizedOutputLease> owner =
			session.AcquireSynchronizedOutputAsync( ownerCancellation.Token ).AsTask();
		TerminalSynchronizedOutputLease? acquiredOwner = null;
		Exception? commitCleanupFailure = null;
		Exception? ownerCleanupFailure = null;
		try {
			Assert.False( commit.IsCompleted );
			Assert.False( owner.IsCompleted );
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => commit.WaitAsync( TimeSpan.FromSeconds( 5 ) )
			);
			Assert.False( owner.IsCompleted );
			Assert.Equal( 0, output.WriteAttemptCount );
			Assert.Equal( 0, output.FlushAttemptCount );
		} finally {
			cancellation.Cancel();
			ownerCancellation.Cancel();
			blockedOutput.Dispose();
			try {
				await commit.WaitAsync( TimeSpan.FromSeconds( 5 ) );
			} catch ( OperationCanceledException ) {
			} catch ( Exception exception ) {
				commitCleanupFailure = exception;
			}
			try {
				acquiredOwner = await owner.WaitAsync( TimeSpan.FromSeconds( 5 ) );
			} catch ( OperationCanceledException ) {
			} catch ( Exception exception ) {
				ownerCleanupFailure = exception;
			}
			if ( acquiredOwner is not null ) {
				try {
					await acquiredOwner.DisposeAsync().AsTask().WaitAsync(
						TimeSpan.FromSeconds( 5 )
					);
				} catch ( Exception exception ) {
					ownerCleanupFailure ??= exception;
				}
			}
		}

		Assert.Null( commitCleanupFailure );
		Assert.Null( ownerCleanupFailure );
		TerminalSynchronizedOutputLease recoveryOwner =
			await session.AcquireSynchronizedOutputAsync().AsTask().WaitAsync(
				TimeSpan.FromSeconds( 5 )
			);
		await recoveryOwner.DisposeAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 5 ) );
	}

	[Fact]
	public async Task CancellationWhileWaitingForSynchronizedReservationEmitsNothing() {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalSynchronizedOutputLease owner =
			await session.AcquireSynchronizedOutputAsync();
		IDisposable blockedOutput = await session.AcquireSessionOutputAsync(
			CancellationToken.None
		);
		Task release = owner.DisposeAsync().AsTask();
		using CancellationTokenSource cancellation = new();
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "cancelled" );

		Task commit = transaction.CommitAsync( cancellation.Token ).AsTask();
		Exception? commitCleanupFailure = null;
		Exception? releaseCleanupFailure = null;
		try {
			Assert.False( release.IsCompleted );
			Assert.False( commit.IsCompleted );
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => commit.WaitAsync( TimeSpan.FromSeconds( 5 ) )
			);
			Assert.Equal( 1, output.WriteAttemptCount );
			Assert.Equal( 0, output.FlushAttemptCount );
		} finally {
			cancellation.Cancel();
			blockedOutput.Dispose();
			try {
				await release.WaitAsync( TimeSpan.FromSeconds( 5 ) );
			} catch ( Exception exception ) {
				releaseCleanupFailure = exception;
			}
			try {
				await commit.WaitAsync( TimeSpan.FromSeconds( 5 ) );
			} catch ( OperationCanceledException ) {
			} catch ( Exception exception ) {
				commitCleanupFailure = exception;
			}
		}

		Assert.Null( releaseCleanupFailure );
		Assert.Null( commitCleanupFailure );
		Assert.Equal( 2, output.WriteAttemptCount );
		Assert.Equal( 1, output.FlushAttemptCount );

		TerminalScreenOutputTransaction recovery = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		recovery.WriteText( "recovery" );
		await recovery.CommitAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 5 ) );
	}

	[Fact]
	public async Task CancellationAfterFirstByteDoesNotTruncateSynchronizedCommit() {
		using CancellationTokenSource cancellation = new();
		RecordingTerminalOutput output = new() {
			AfterSuccessfulWrite = attempt => {
				if ( 1 == attempt ) {
					cancellation.Cancel();
				}
			}
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "first" );
		transaction.WriteText( "second" );

		await transaction.CommitAsync( cancellation.Token );

		Assert.True( cancellation.IsCancellationRequested );
		Assert.Equal(
			[
				.. CsiWriter.EncodeSynchronizedOutputBeginFrame(),
				.. "firstsecond"u8.ToArray(),
				.. CsiWriter.EncodeSynchronizedOutputEndFrame()
			],
			output.GetCombinedWrites()
		);
		Assert.Equal( 4, output.WriteAttemptCount );
		Assert.All( output.WriteCancellationCanBeCanceled, Assert.False );
		Assert.Equal( 1, output.FlushAttemptCount );
		Assert.False( output.FlushCancellationCanBeCanceled[ 0 ] );
	}

	[Theory]
	[InlineData( 1, 2, "Synthetic write failure 1." )]
	[InlineData( 2, 3, "Synthetic write failure 2." )]
	[InlineData( 3, 3, "Synthetic write failure 3." )]
	public async Task SynchronizedWriteFailureAttemptsCleanupAndAllowsRecovery(
		int failingWriteAttempt,
		int expectedWriteAttempts,
		string expectedMessage
	) {
		RecordingTerminalOutput output = new() {
			FailingWriteAttempts = [ failingWriteAttempt ]
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction stale =
			session.CreateScreenOutputTransaction();
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "body" );

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => transaction.CommitAsync().AsTask()
		);

		Assert.Equal( expectedMessage, exception.Message );
		Assert.Equal( expectedWriteAttempts, output.WriteAttemptCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			output.GetWrite( expectedWriteAttempts - 1 )
		);
		Assert.All( output.WriteCancellationCanBeCanceled, Assert.False );
		Assert.Equal( 1, output.FlushAttemptCount );
		Assert.False( output.FlushCancellationCanBeCanceled[ 0 ] );
		InvalidOperationException second = await Assert.ThrowsAsync<InvalidOperationException>(
			() => transaction.CommitAsync().AsTask()
		);
		Assert.Equal(
			"A screen-output transaction can be committed only once.",
			second.Message
		);
		Assert.Equal( expectedWriteAttempts, output.WriteAttemptCount );
		Assert.Equal( 1, output.FlushAttemptCount );
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => stale.CommitAsync().AsTask()
		);

		output.ClearFailures();
		TerminalScreenOutputTransaction recovery = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		recovery.WriteText( "recovery" );
		await recovery.CommitAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 5 ) );
		Assert.Equal( expectedWriteAttempts + 3, output.WriteAttemptCount );
		Assert.Equal( 2, output.FlushAttemptCount );
	}

	[Fact]
	public async Task SynchronizedFlushFailureReleasesGateAndAllowsRecovery() {
		RecordingTerminalOutput output = new() {
			FailingFlushAttempts = [ 1 ]
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "body" );

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => transaction.CommitAsync().AsTask()
		);

		Assert.Equal( "Synthetic flush failure 1.", exception.Message );
		Assert.Equal( 3, output.WriteAttemptCount );
		Assert.Equal( 1, output.FlushAttemptCount );
		Assert.False( output.FlushCancellationCanBeCanceled[ 0 ] );

		output.ClearFailures();
		TerminalScreenOutputTransaction recovery =
			session.CreateScreenOutputTransaction();
		recovery.WriteText( "recovery" );
		await recovery.CommitAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 5 ) );
		Assert.Equal( 4, output.WriteAttemptCount );
		Assert.Equal( 2, output.FlushAttemptCount );
	}

	[Fact]
	public async Task IndependentPrimaryAndCleanupFailuresAreAggregatedInOrder() {
		RecordingTerminalOutput output = new() {
			FailingWriteAttempts = [ 2, 3 ],
			FailingFlushAttempts = [ 1 ]
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "body" );

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => transaction.CommitAsync().AsTask()
		);

		Assert.Equal( 3, exception.InnerExceptions.Count );
		Assert.Collection(
			exception.InnerExceptions,
			failure => Assert.Equal( "Synthetic write failure 2.", failure.Message ),
			failure => Assert.Equal( "Synthetic write failure 3.", failure.Message ),
			failure => Assert.Equal( "Synthetic flush failure 1.", failure.Message )
		);
		Assert.All( exception.InnerExceptions, failure => Assert.IsType<IOException>( failure ) );
		Assert.Equal( 3, output.WriteAttemptCount );
		Assert.Equal( 1, output.FlushAttemptCount );
		Assert.All( output.WriteCancellationCanBeCanceled, Assert.False );
		Assert.False( output.FlushCancellationCanBeCanceled[ 0 ] );

		output.ClearFailures();
		TerminalScreenOutputTransaction recovery = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		recovery.WriteText( "recovery" );
		await recovery.CommitAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 5 ) );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );
		TerminalDescription terminal = new TerminalDescriptionBuilder( "screen-output-hardening" )
			.SetString( StringCapability.Bell, "<bell>" )
			.Build();
		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class TestTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		private readonly object sync = new();
		private readonly List<byte[]> writes = [];
		private readonly List<bool> writeCancellationCanBeCanceled = [];
		private readonly List<bool> flushCancellationCanBeCanceled = [];
		private readonly TaskCompletionSource paused = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private readonly TaskCompletionSource releasePause = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private int writeAttemptCount;
		private int flushAttemptCount;

		internal HashSet<int> FailingWriteAttempts { get; init; } = [];

		internal HashSet<int> FailingFlushAttempts { get; init; } = [];

		internal int PauseAfterWriteAttempt { get; init; }

		internal Action<int>? AfterSuccessfulWrite { get; init; }

		internal int WriteAttemptCount {
			get {
				return Volatile.Read( ref this.writeAttemptCount );
			}
		}

		internal int FlushAttemptCount {
			get {
				return Volatile.Read( ref this.flushAttemptCount );
			}
		}

		internal bool[] WriteCancellationCanBeCanceled {
			get {
				lock ( this.sync ) {
					return this.writeCancellationCanBeCanceled.ToArray();
				}
			}
		}

		internal bool[] FlushCancellationCanBeCanceled {
			get {
				lock ( this.sync ) {
					return this.flushCancellationCanBeCanceled.ToArray();
				}
			}
		}

		public async ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			int attempt = Interlocked.Increment( ref this.writeAttemptCount );
			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
				this.writeCancellationCanBeCanceled.Add( cancellationToken.CanBeCanceled );
			}
			if ( this.FailingWriteAttempts.Contains( attempt ) ) {
				throw new IOException( $"Synthetic write failure {attempt}." );
			}
			this.AfterSuccessfulWrite?.Invoke( attempt );
			if ( this.PauseAfterWriteAttempt == attempt ) {
				this.paused.TrySetResult();
				await this.releasePause.Task.WaitAsync( cancellationToken ).ConfigureAwait( false );
			}
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			int attempt = Interlocked.Increment( ref this.flushAttemptCount );
			lock ( this.sync ) {
				this.flushCancellationCanBeCanceled.Add( cancellationToken.CanBeCanceled );
			}
			if ( this.FailingFlushAttempts.Contains( attempt ) ) {
				return ValueTask.FromException(
					new IOException( $"Synthetic flush failure {attempt}." )
				);
			}
			return ValueTask.CompletedTask;
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.sync ) {
				return this.writes[ index ].ToArray();
			}
		}

		internal byte[] GetCombinedWrites() {
			lock ( this.sync ) {
				return this.writes.SelectMany( static write => write ).ToArray();
			}
		}

		internal Task WaitUntilPausedAsync() {
			if ( 0 >= this.PauseAfterWriteAttempt ) {
				throw new InvalidOperationException( "No output pause is configured." );
			}
			return this.paused.Task;
		}

		internal void ReleasePause() {
			this.releasePause.TrySetResult();
		}

		internal void ClearFailures() {
			this.FailingWriteAttempts.Clear();
			this.FailingFlushAttempts.Clear();
		}
	}

	private sealed class TestTerminalControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0,
			0,
			0,
			0x0002UL,
			new byte[ 32 ],
			0,
			32,
			0,
			new TerminalSpeed( 13, 9600 ),
			new TerminalSpeed( 13, 9600 )
		);

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					true,
					null,
					TerminalPlatformKind.PosixTermios,
					TerminalControlCapabilities.Attachment
						| TerminalControlCapabilities.ModeRead
						| TerminalControlCapabilities.ModeWrite
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalSize>.Unavailable(
				"No scripted live size."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available(
				this.baseline
			);
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			ArgumentNullException.ThrowIfNull( mode );
			if ( !Enum.IsDefined( timing ) ) {
				throw new ArgumentOutOfRangeException( nameof( timing ) );
			}
			return TerminalControlMutationResult.Success();
		}
	}
}
