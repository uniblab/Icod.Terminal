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
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>Hardens T177 hyperlink and raster screen-output composition.</summary>
public sealed class TerminalScreenOutputCompositionTests {
	[Fact]
	public async Task RasterAddRejectsDefaultForeignAndUnregisteredCellsWithoutMutation() {
		ScriptedTransport firstTransport = new();
		ScriptedTransport secondTransport = new();
		await using TerminalSession first = await OpenSessionAsync( firstTransport );
		await using TerminalSession second = await OpenSessionAsync( secondTransport );
		(
			TerminalRasterResource firstResource,
			TerminalRasterPlaceholder firstPlaceholder
		) = await CreatePlaceholderAsync( first, firstTransport, 71u );
		await using ( firstResource ) {
			await using ( firstPlaceholder ) {
				(
					TerminalRasterResource secondResource,
					TerminalRasterPlaceholder secondPlaceholder
				) = await CreatePlaceholderAsync( second, secondTransport, 72u );
				await using ( secondResource ) {
					await using ( secondPlaceholder ) {
						TerminalPersistentRasterResourceState forgedResource = new(
							imageNumber: 99u,
							generation: 99L
						);
						forgedResource.BindImageId( 99u );
						TerminalPersistentRasterPlaceholderState forgedState = new(
							forgedResource,
							placementId: 99u,
							generation: 99L,
							columns: 1,
							rows: 1
						);
						await using TerminalRasterPlaceholder unregistered = new(
							second,
							forgedState
						);
						TerminalScreenOutputTransaction transaction =
							second.CreateScreenOutputTransaction();

						ArgumentException defaultFailure = Assert.Throws<ArgumentException>(
							() => transaction.WriteRasterPlaceholderCell( default )
						);
						ArgumentException foreignFailure = Assert.Throws<ArgumentException>(
							() => transaction.WriteRasterPlaceholderCell(
								firstPlaceholder.GetCell( 0, 0 )
							)
						);
						InvalidOperationException unregisteredFailure =
							Assert.Throws<InvalidOperationException>(
								() => transaction.WriteRasterPlaceholderCell(
									unregistered.GetCell( 0, 0 )
								)
						);
						int baselineWrites = secondTransport.WriteAttemptCount;
						int baselineFlushes = secondTransport.FlushAttemptCount;
						transaction.WriteRasterPlaceholderCell(
							secondPlaceholder.GetCell( 0, 0 )
						);
						await transaction.CommitAsync();

						Assert.Equal( "cell", defaultFailure.ParamName );
						Assert.Equal( "cell", foreignFailure.ParamName );
						Assert.Contains( "current registered session generation", unregisteredFailure.Message );
						Assert.Equal( baselineWrites + 1, secondTransport.WriteAttemptCount );
						Assert.Equal( baselineFlushes + 1, secondTransport.FlushAttemptCount );
					}
				}
			}
		}
	}

	[Fact]
	public async Task MixedRasterBatchRejectsAtomicallyAndRepeatedCellsRetainOrder() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		(
			TerminalRasterResource resource,
			TerminalRasterPlaceholder placeholder
		) = await CreatePlaceholderAsync( session, transport, 73u, columns: 2 );
		await using ( resource ) {
			await using ( placeholder ) {
				TerminalRasterPlaceholderCell first = placeholder.GetCell( 0, 0 );
				TerminalRasterPlaceholderCell second = placeholder.GetCell( 0, 1 );
				TerminalScreenOutputTransaction transaction =
					session.CreateScreenOutputTransaction();

				Assert.Throws<ArgumentException>(
					() => transaction.WriteRasterPlaceholderCells(
						new TerminalRasterPlaceholderCell[] { first, default, second }
					)
				);
				transaction.WriteRasterPlaceholderCells(
					new TerminalRasterPlaceholderCell[] { second, first, second }
				);
				int baselineWrites = transport.WriteAttemptCount;
				int baselineFlushes = transport.FlushAttemptCount;

				await transaction.CommitAsync();

				Assert.Equal( baselineWrites + 3, transport.WriteAttemptCount );
				Assert.Equal(
					KittyGraphicsPlaceholderCellEncoder.Encode(
						placeholder.State,
						0,
						1
					).ToArray(),
					transport.GetWrite( baselineWrites )
				);
				Assert.Equal(
					KittyGraphicsPlaceholderCellEncoder.Encode(
						placeholder.State,
						0,
						0
					).ToArray(),
					transport.GetWrite( baselineWrites + 1 )
				);
				Assert.Equal( transport.GetWrite( baselineWrites ), transport.GetWrite( baselineWrites + 2 ) );
				Assert.Equal( baselineFlushes + 1, transport.FlushAttemptCount );
			}
		}
	}

	[Fact]
	public async Task DisposedRasterCellInvalidatesMixedCommitBeforeAnyTransactionOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		(
			TerminalRasterResource resource,
			TerminalRasterPlaceholder placeholder
		) = await CreatePlaceholderAsync( session, transport, 74u, columns: 2 );
		await using ( resource ) {
			TerminalRasterPlaceholderCell first = placeholder.GetCell( 0, 0 );
			TerminalRasterPlaceholderCell second = placeholder.GetCell( 0, 1 );
			TerminalScreenOutputTransaction transaction =
				session.CreateScreenOutputTransaction();
			transaction.WriteText( "must-not-emit" );
			transaction.WriteRasterPlaceholderCells(
				new TerminalRasterPlaceholderCell[] { first, second }
			);
			await placeholder.DisposeAsync();
			int baselineWrites = transport.WriteAttemptCount;
			int baselineFlushes = transport.FlushAttemptCount;

			await Assert.ThrowsAsync<ObjectDisposedException>(
				() => transaction.CommitAsync().AsTask()
			);

			Assert.Equal( baselineWrites, transport.WriteAttemptCount );
			Assert.Equal( baselineFlushes, transport.FlushAttemptCount );
		}
	}

	[Fact]
	public async Task ResourceInvalidationWhileCommitWaitsForGateFailsAtomicallyAndAllowsRecovery() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		(
			TerminalRasterResource resource,
			TerminalRasterPlaceholder placeholder
		) = await CreatePlaceholderAsync( session, transport, 75u );
		await using ( resource ) {
			await using ( placeholder ) {
				IDisposable outputBlocker = await session.AcquireSessionOutputAsync(
					CancellationToken.None
				);
				TerminalScreenOutputTransaction transaction =
					session.CreateScreenOutputTransaction();
				transaction.WriteText( "must-not-emit" );
				transaction.WriteRasterPlaceholderCell( placeholder.GetCell( 0, 0 ) );
				int baselineWrites = transport.WriteAttemptCount;
				int baselineFlushes = transport.FlushAttemptCount;
				Task commit = transaction.CommitAsync().AsTask();
				Exception? failure = null;
				try {
					Assert.False( commit.IsCompleted );
					Assert.True(
						session.InvalidatePersistentRasterResourceWithVirtualDescendants(
							placeholder.State.Resource
						)
					);
				} finally {
					outputBlocker.Dispose();
					try {
						await commit.WaitAsync( TimeSpan.FromSeconds( 5 ) );
					} catch ( Exception exception ) {
						failure = exception;
					}
				}

				Assert.IsType<InvalidOperationException>( failure );
				Assert.Equal( baselineWrites, transport.WriteAttemptCount );
				Assert.Equal( baselineFlushes, transport.FlushAttemptCount );

				TerminalScreenOutputTransaction recovery =
					session.CreateScreenOutputTransaction();
				recovery.WriteText( "recovery" );
				await recovery.CommitAsync();
				Assert.Equal( "recovery"u8.ToArray(), transport.GetWrite( baselineWrites ) );
			}
		}
	}

	[Fact]
	public async Task HyperlinkItemWritesBeginTextEndThenFlushInExactOrder() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.WriteText( "before" );
		transaction.WriteHyperlink(
			"linked",
			"https://example.com/path",
			"screen"
		);
		transaction.WriteText( "after" );

		await transaction.CommitAsync();

		Assert.Equal( 5, transport.WriteAttemptCount );
		Assert.Equal( "before"u8.ToArray(), transport.GetWrite( 0 ) );
		Assert.Equal(
			OscWriter.EncodeHyperlinkBeginFrame(
				"https://example.com/path",
				"screen"
			),
			transport.GetWrite( 1 )
		);
		Assert.Equal( "linked"u8.ToArray(), transport.GetWrite( 2 ) );
		Assert.Equal( OscWriter.EncodeHyperlinkEndFrame(), transport.GetWrite( 3 ) );
		Assert.Equal( "after"u8.ToArray(), transport.GetWrite( 4 ) );
		Assert.Equal( 1, transport.FlushAttemptCount );
	}

	[Theory]
	[InlineData( 2, 4, 3 )]
	[InlineData( 3, 5, 4 )]
	[InlineData( 4, 5, 4 )]
	public async Task HyperlinkFailureAttemptsRequiredEndAndFlushThenAllowsRecovery(
		int failingWriteAttempt,
		int expectedWriteAttempts,
		int expectedHyperlinkEndAttempt
	) {
		ScriptedTransport transport = new() {
			FailingWriteAttempts = [ failingWriteAttempt ]
		};
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteHyperlink( "linked", "https://example.com/" );
		transaction.WriteText( "must-not-emit" );

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => transaction.CommitAsync().AsTask()
		);

		Assert.Equal( $"Synthetic write failure {failingWriteAttempt}.", exception.Message );
		Assert.Equal( expectedWriteAttempts, transport.WriteAttemptCount );
		Assert.Equal(
			OscWriter.EncodeHyperlinkEndFrame(),
			transport.GetWrite( expectedHyperlinkEndAttempt - 1 )
		);
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			transport.GetWrite( expectedWriteAttempts - 1 )
		);
		Assert.DoesNotContain(
			transport.Writes,
			write => write.AsSpan().SequenceEqual( "must-not-emit"u8 )
		);
		Assert.Equal( 1, transport.FlushAttemptCount );
		Assert.All( transport.WriteCancellationCanBeCanceled, Assert.False );
		Assert.All( transport.FlushCancellationCanBeCanceled, Assert.False );

		transport.ClearFailures();
		TerminalScreenOutputTransaction recovery =
			session.CreateScreenOutputTransaction();
		recovery.WriteText( "recovery" );
		await recovery.CommitAsync();
	}

	[Fact]
	public async Task ComposedCleanupFailuresAreAggregatedInAttemptOrder() {
		ScriptedTransport transport = new() {
			FailingWriteAttempts = [ 3, 4, 5 ],
			FailingFlushAttempts = [ 1 ]
		};
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalScreenOutputTransaction stale =
			session.CreateScreenOutputTransaction();
		stale.WriteText( "stale" );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteHyperlink( "linked", "https://example.com/" );
		transaction.WriteText( "must-not-emit" );

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => transaction.CommitAsync().AsTask()
		);

		Assert.Collection(
			exception.InnerExceptions,
			failure => Assert.Equal( "Synthetic write failure 3.", failure.Message ),
			failure => Assert.Equal( "Synthetic write failure 4.", failure.Message ),
			failure => Assert.Equal( "Synthetic write failure 5.", failure.Message ),
			failure => Assert.Equal( "Synthetic flush failure 1.", failure.Message )
		);
		Assert.All( exception.InnerExceptions, failure => Assert.IsType<IOException>( failure ) );
		Assert.Equal( 5, transport.WriteAttemptCount );
		Assert.Equal(
			CsiWriter.EncodeSynchronizedOutputEndFrame(),
			transport.GetWrite( 4 )
		);
		Assert.DoesNotContain(
			transport.Writes,
			write => write.AsSpan().SequenceEqual( "must-not-emit"u8 )
		);
		Assert.Equal( 1, transport.FlushAttemptCount );
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => stale.CommitAsync().AsTask()
		);

		transport.ClearFailures();
		TerminalScreenOutputTransaction recovery = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		recovery.WriteText( "recovery" );
		await recovery.CommitAsync();
	}

	[Fact]
	public async Task ActiveHyperlinkOwnershipRejectsTransactionBeforeBytesThenRecovers() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalHyperlinkLease owner = await session.AcquireHyperlinkAsync(
			"https://example.com/owner"
		);
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.WriteHyperlink( "blocked", "https://example.com/transaction" );
		int baselineWrites = transport.WriteAttemptCount;
		int baselineFlushes = transport.FlushAttemptCount;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => transaction.CommitAsync().AsTask()
		);
		Assert.Equal( baselineWrites, transport.WriteAttemptCount );
		Assert.Equal( baselineFlushes, transport.FlushAttemptCount );

		await owner.DisposeAsync();
		TerminalScreenOutputTransaction recovery =
			session.CreateScreenOutputTransaction();
		recovery.WriteHyperlink( "recovery", "https://example.com/recovery" );
		await recovery.CommitAsync();
	}

	[Fact]
	public async Task PendingHyperlinkCleanupRejectsTransactionBeforeBytesThenRecovers() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalHyperlinkLease owner = await session.AcquireHyperlinkAsync(
			"https://example.com/owner"
		);
		transport.FailingWriteAttempts.Add( transport.WriteAttemptCount + 1 );
		await Assert.ThrowsAsync<IOException>( () => owner.DisposeAsync().AsTask() );
		TerminalScreenOutputTransaction transaction =
			session.CreateScreenOutputTransaction();
		transaction.WriteHyperlink( "blocked", "https://example.com/transaction" );
		int baselineWrites = transport.WriteAttemptCount;
		int baselineFlushes = transport.FlushAttemptCount;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => transaction.CommitAsync().AsTask()
		);
		Assert.Equal( baselineWrites, transport.WriteAttemptCount );
		Assert.Equal( baselineFlushes, transport.FlushAttemptCount );

		transport.ClearFailures();
		await owner.DisposeAsync();
		TerminalScreenOutputTransaction recovery =
			session.CreateScreenOutputTransaction();
		recovery.WriteHyperlink( "recovery", "https://example.com/recovery" );
		await recovery.CommitAsync();
	}

	[Fact]
	public async Task ActiveSynchronizedOwnershipRejectsTransactionBeforeBytesThenRecovers() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalSynchronizedOutputLease owner =
			await session.AcquireSynchronizedOutputAsync();
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "blocked" );
		int baselineWrites = transport.WriteAttemptCount;
		int baselineFlushes = transport.FlushAttemptCount;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => transaction.CommitAsync().AsTask()
		);
		Assert.Equal( baselineWrites, transport.WriteAttemptCount );
		Assert.Equal( baselineFlushes, transport.FlushAttemptCount );

		await owner.DisposeAsync();
		TerminalScreenOutputTransaction recovery = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		recovery.WriteText( "recovery" );
		await recovery.CommitAsync();
	}

	[Fact]
	public async Task PendingSynchronizedCleanupRejectsTransactionBeforeBytesThenRecovers() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalSynchronizedOutputLease owner =
			await session.AcquireSynchronizedOutputAsync();
		transport.FailingFlushAttempts.Add( transport.FlushAttemptCount + 1 );
		await Assert.ThrowsAsync<IOException>( () => owner.DisposeAsync().AsTask() );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteText( "blocked" );
		int baselineWrites = transport.WriteAttemptCount;
		int baselineFlushes = transport.FlushAttemptCount;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => transaction.CommitAsync().AsTask()
		);
		Assert.Equal( baselineWrites, transport.WriteAttemptCount );
		Assert.Equal( baselineFlushes, transport.FlushAttemptCount );

		transport.ClearFailures();
		await owner.DisposeAsync();
		TerminalScreenOutputTransaction recovery = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		recovery.WriteText( "recovery" );
		await recovery.CommitAsync();
	}

	[Fact]
	public async Task PreCancelledComposedCommitAcquiresNoReservationAndEmitsNothing() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteHyperlink( "cancelled", "https://example.com/" );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => transaction.CommitAsync( cancellation.Token ).AsTask()
		);

		Assert.Equal( 0, transport.WriteAttemptCount );
		Assert.Equal( 0, transport.FlushAttemptCount );
		using IDisposable hyperlink = await session.ReserveScreenHyperlinkOutputAsync(
			CancellationToken.None
		);
		using IDisposable synchronized = await session.SynchronizedOutputManager.ReserveScreenOutputAsync(
			CancellationToken.None
		);
	}

	[Fact]
	public async Task CancellationWhileWaitingForHyperlinkReservationReleasesEverything() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		IDisposable blocker = await session.ReserveScreenHyperlinkOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteHyperlink( "cancelled", "https://example.com/" );
		Task commit = transaction.CommitAsync( cancellation.Token ).AsTask();
		try {
			Assert.False( commit.IsCompleted );
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => commit.WaitAsync( TimeSpan.FromSeconds( 5 ) )
			);
		} finally {
			cancellation.Cancel();
			blocker.Dispose();
			try {
				await commit.WaitAsync( TimeSpan.FromSeconds( 5 ) );
			} catch ( OperationCanceledException ) {
			}
		}

		Assert.Equal( 0, transport.WriteAttemptCount );
		Assert.Equal( 0, transport.FlushAttemptCount );
		TerminalScreenOutputTransaction recovery = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		recovery.WriteHyperlink( "recovery", "https://example.com/recovery" );
		await recovery.CommitAsync();
	}

	[Fact]
	public async Task CancellationWhileWaitingForSynchronizedReservationReleasesHyperlinkReservation() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		IDisposable synchronizedBlocker = await session.SynchronizedOutputManager.ReserveScreenOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();
		using CancellationTokenSource probeCancellation = new();
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteHyperlink( "cancelled", "https://example.com/" );
		Task commit = transaction.CommitAsync( cancellation.Token ).AsTask();
		Task<IDisposable> hyperlinkProbe = session.ReserveScreenHyperlinkOutputAsync(
			probeCancellation.Token
		).AsTask();
		IDisposable? hyperlinkReservation = null;
		Exception? commitCleanupFailure = null;
		Exception? probeCleanupFailure = null;
		try {
			Assert.False( commit.IsCompleted );
			Assert.False( hyperlinkProbe.IsCompleted );
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => commit.WaitAsync( TimeSpan.FromSeconds( 5 ) )
			);
			hyperlinkReservation = await hyperlinkProbe.WaitAsync( TimeSpan.FromSeconds( 5 ) );
		} finally {
			cancellation.Cancel();
			probeCancellation.Cancel();
			synchronizedBlocker.Dispose();
			commitCleanupFailure = await CaptureFailureAsync( commit );
			var probeCompletion = await CaptureCompletionAsync(
				hyperlinkProbe
			);
			hyperlinkReservation ??= probeCompletion.Value;
			probeCleanupFailure = probeCompletion.Failure;
			hyperlinkReservation?.Dispose();
		}

		Assert.IsAssignableFrom<OperationCanceledException>( commitCleanupFailure );
		Assert.Null( probeCleanupFailure );
		Assert.Equal( 0, transport.WriteAttemptCount );
		Assert.Equal( 0, transport.FlushAttemptCount );
	}

	[Fact]
	public async Task CancellationWhileWaitingForOutputGateReleasesBothReservationsAndPreservesSibling() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		IDisposable outputBlocker = await session.AcquireSessionOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();
		using CancellationTokenSource probeCancellation = new();
		TerminalScreenOutputTransaction cancelled = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		cancelled.WriteHyperlink( "cancelled", "https://example.com/cancelled" );
		TerminalScreenOutputTransaction sibling = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		sibling.WriteHyperlink( "sibling", "https://example.com/sibling" );
		Task commit = cancelled.CommitAsync( cancellation.Token ).AsTask();
		Task<IDisposable> hyperlinkProbe = session.ReserveScreenHyperlinkOutputAsync(
			probeCancellation.Token
		).AsTask();
		Task<IDisposable> synchronizedProbe = session.SynchronizedOutputManager.ReserveScreenOutputAsync(
			probeCancellation.Token
		).AsTask();
		IDisposable? hyperlinkReservation = null;
		IDisposable? synchronizedReservation = null;
		Exception? commitCleanupFailure = null;
		Exception? hyperlinkProbeCleanupFailure = null;
		Exception? synchronizedProbeCleanupFailure = null;
		try {
			Assert.False( commit.IsCompleted );
			Assert.False( hyperlinkProbe.IsCompleted );
			Assert.False( synchronizedProbe.IsCompleted );
			cancellation.Cancel();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => commit.WaitAsync( TimeSpan.FromSeconds( 5 ) )
			);
			hyperlinkReservation = await hyperlinkProbe.WaitAsync( TimeSpan.FromSeconds( 5 ) );
			synchronizedReservation = await synchronizedProbe.WaitAsync( TimeSpan.FromSeconds( 5 ) );
			Assert.Equal( 0, transport.WriteAttemptCount );
			Assert.Equal( 0, transport.FlushAttemptCount );
		} finally {
			cancellation.Cancel();
			probeCancellation.Cancel();
			outputBlocker.Dispose();
			commitCleanupFailure = await CaptureFailureAsync( commit );
			var hyperlinkCompletion = await CaptureCompletionAsync( hyperlinkProbe );
			var synchronizedCompletion = await CaptureCompletionAsync( synchronizedProbe );
			hyperlinkReservation ??= hyperlinkCompletion.Value;
			synchronizedReservation ??= synchronizedCompletion.Value;
			hyperlinkProbeCleanupFailure = hyperlinkCompletion.Failure;
			synchronizedProbeCleanupFailure = synchronizedCompletion.Failure;
			hyperlinkReservation?.Dispose();
			synchronizedReservation?.Dispose();
		}

		Assert.IsAssignableFrom<OperationCanceledException>( commitCleanupFailure );
		Assert.Null( hyperlinkProbeCleanupFailure );
		Assert.Null( synchronizedProbeCleanupFailure );
		using CancellationTokenSource siblingCancellation = new();
		Task siblingCommit = sibling.CommitAsync( siblingCancellation.Token ).AsTask();
		Exception? siblingCleanupFailure = null;
		try {
			await siblingCommit.WaitAsync( TimeSpan.FromSeconds( 5 ) );
		} finally {
			siblingCancellation.Cancel();
			siblingCleanupFailure = await CaptureFailureAsync( siblingCommit );
		}
		Assert.Null( siblingCleanupFailure );
		Assert.Equal( 5, transport.WriteAttemptCount );
		Assert.Equal( 1, transport.FlushAttemptCount );
	}

	[Fact]
	public async Task CancellationAfterFirstCommittedByteDoesNotTruncateComposedCleanup() {
		using CancellationTokenSource cancellation = new();
		ScriptedTransport transport = new() {
			AfterSuccessfulWrite = attempt => {
				if ( 1 == attempt ) {
					cancellation.Cancel();
				}
			}
		};
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteHyperlink( "linked", "https://example.com/" );
		transaction.WriteText( "after" );

		await transaction.CommitAsync( cancellation.Token );

		Assert.True( cancellation.IsCancellationRequested );
		Assert.Equal( 6, transport.WriteAttemptCount );
		Assert.Equal( CsiWriter.EncodeSynchronizedOutputBeginFrame(), transport.GetWrite( 0 ) );
		Assert.Equal( OscWriter.EncodeHyperlinkEndFrame(), transport.GetWrite( 3 ) );
		Assert.Equal( "after"u8.ToArray(), transport.GetWrite( 4 ) );
		Assert.Equal( CsiWriter.EncodeSynchronizedOutputEndFrame(), transport.GetWrite( 5 ) );
		Assert.All( transport.WriteCancellationCanBeCanceled, Assert.False );
		Assert.All( transport.FlushCancellationCanBeCanceled, Assert.False );
	}

	[Fact]
	public async Task ReservationsUseHyperlinkThenSynchronizedThenOutputOrderWithoutDeadlock() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		IDisposable synchronizedBlocker = await session.SynchronizedOutputManager.ReserveScreenOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource commitCancellation = new();
		using CancellationTokenSource ownerCancellation = new();
		TerminalScreenOutputTransaction transaction = session.CreateScreenOutputTransaction(
			new TerminalScreenOutputTransactionOptions { UseSynchronizedOutput = true }
		);
		transaction.WriteHyperlink( "transaction", "https://example.com/transaction" );
		Task commit = transaction.CommitAsync( commitCancellation.Token ).AsTask();
		Task<TerminalHyperlinkLease> ownerTask = session.AcquireHyperlinkAsync(
			"https://example.com/owner",
			cancellationToken: ownerCancellation.Token
		).AsTask();
		TerminalHyperlinkLease? owner = null;
		Exception? ownerCleanupFailure = null;
		Exception? ownerDisposeFailure = null;
		Exception? commitCleanupFailure = null;
		try {
			Assert.False( commit.IsCompleted );
			Assert.False( ownerTask.IsCompleted );
			synchronizedBlocker.Dispose();
			await commit.WaitAsync( TimeSpan.FromSeconds( 5 ) );
			owner = await ownerTask.WaitAsync( TimeSpan.FromSeconds( 5 ) );

			Assert.Equal( CsiWriter.EncodeSynchronizedOutputBeginFrame(), transport.GetWrite( 0 ) );
			Assert.Equal( CsiWriter.EncodeSynchronizedOutputEndFrame(), transport.GetWrite( 4 ) );
			Assert.Equal(
				OscWriter.EncodeHyperlinkBeginFrame( "https://example.com/owner" ),
				transport.GetWrite( 5 )
			);
		} finally {
			commitCancellation.Cancel();
			ownerCancellation.Cancel();
			synchronizedBlocker.Dispose();
			var ownerCompletion = await CaptureCompletionAsync( ownerTask );
			owner ??= ownerCompletion.Value;
			ownerCleanupFailure = ownerCompletion.Failure;
			if ( owner is not null ) {
				ownerDisposeFailure = await CaptureFailureAsync(
					owner.DisposeAsync().AsTask()
				);
			}
			commitCleanupFailure = await CaptureFailureAsync( commit );
		}

		Assert.Null( ownerCleanupFailure );
		Assert.Null( ownerDisposeFailure );
		Assert.Null( commitCleanupFailure );
	}

	private static async Task<Exception?> CaptureFailureAsync(
		Task task
	) {
		try {
			await task.ConfigureAwait( false );
			return null;
		} catch ( Exception exception ) {
			return exception;
		}
	}

	private static async Task<(T? Value, Exception? Failure)> CaptureCompletionAsync<T>(
		Task<T> task
	) where T : class {
		try {
			return ( await task.ConfigureAwait( false ), null );
		} catch ( Exception exception ) {
			return ( null, exception );
		}
	}

	private static async Task<(
		TerminalRasterResource Resource,
		TerminalRasterPlaceholder Placeholder
	)> CreatePlaceholderAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId,
		int columns = 1,
		int rows = 1
	) {
		int resourceWrite = transport.WriteAttemptCount + 1;
		Task<TerminalControlResult<TerminalRasterResource>> resourceTask =
			session.CreateRasterResourceAsync(
				TerminalRasterImage.CreateRgb24(
					1,
					1,
					[ 1, 2, 3 ]
				)
			).AsTask();
		await transport.WaitForWriteCountAsync( resourceWrite );
		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b_Gi={imageId},I=1;OK\u001b\\" )
		);
		TerminalRasterResource resource = Assert.IsType<TerminalRasterResource>(
			( await resourceTask ).Value
		);

		int placeholderWrite = transport.WriteAttemptCount + 1;
		Task<TerminalControlResult<TerminalRasterPlaceholder>> placeholderTask =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = columns,
					Rows = rows
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( placeholderWrite );
		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b_Gi={imageId},p=1;OK\u001b\\" )
		);
		TerminalRasterPlaceholder placeholder = Assert.IsType<TerminalRasterPlaceholder>(
			( await placeholderTask ).Value
		);
		return ( resource, placeholder );
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		TerminalSession session = await TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			}
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		return session;
	}

	private sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly object sync = new();
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private readonly List<byte[]> writes = [];
		private readonly List<bool> writeCancellationCanBeCanceled = [];
		private readonly List<bool> flushCancellationCanBeCanceled = [];
		private int writeAttemptCount;
		private int flushAttemptCount;

		internal HashSet<int> FailingWriteAttempts { get; init; } = [];

		internal HashSet<int> FailingFlushAttempts { get; init; } = [];

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

		internal byte[][] Writes {
			get {
				lock ( this.sync ) {
					return this.writes.Select( write => write.ToArray() ).ToArray();
				}
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] value = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( value.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted response exceeds the terminal input buffer."
				);
			}
			value.AsSpan().CopyTo( buffer.Span );
			return value.Length;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			int attempt = Interlocked.Increment( ref this.writeAttemptCount );
			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
				this.writeCancellationCanBeCanceled.Add( cancellationToken.CanBeCanceled );
			}
			this.writeSignal.Release();
			if ( this.FailingWriteAttempts.Contains( attempt ) ) {
				return ValueTask.FromException(
					new IOException( $"Synthetic write failure {attempt}." )
				);
			}
			this.AfterSuccessfulWrite?.Invoke( attempt );
			return ValueTask.CompletedTask;
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

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel is closed."
				);
			}
		}

		internal async Task WaitForWriteCountAsync(
			int expected
		) {
			using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
			while ( this.WriteAttemptCount < expected ) {
				await this.writeSignal.WaitAsync( timeout.Token ).ConfigureAwait( false );
			}
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
