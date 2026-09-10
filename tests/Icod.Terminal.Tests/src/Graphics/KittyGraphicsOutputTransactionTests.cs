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
namespace Icod.Terminal.Tests.Graphics;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies A184 committed, serialized, multi-frame Kitty Graphics output semantics.
/// </summary>
public sealed class KittyGraphicsOutputTransactionTests {
	[Fact]
	public async Task PreCancelledTransactionWritesNothing() {
		ScriptedOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		KittyRasterData raster = CreateSmallRaster();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => KittyGraphicsOutputTransaction.WriteAsync(
				session,
				raster,
				cancellation.Token
			).AsTask()
		);

		Assert.Equal( 0, output.WriteAttemptCount );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task CancellationWhileWaitingForOutputGateWritesNothing() {
		ScriptedOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		KittyRasterData raster = CreateSmallRaster();
		using IDisposable lease = await session.AcquireSessionOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();

		Task transaction = KittyGraphicsOutputTransaction.WriteAsync(
			session,
			raster,
			cancellation.Token
		).AsTask();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => transaction
		);
		Assert.Equal( 0, output.WriteAttemptCount );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task CancellationAfterFirstFrameCommitCompletesAllChunks() {
		using CancellationTokenSource cancellation = new();
		ScriptedOutput output = new() {
			AfterSuccessfulWrite = attempt => {
				if ( 1 == attempt ) {
					cancellation.Cancel();
				}
			}
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		KittyRasterData raster = CreateTwoChunkRaster();
		byte[] expected = CreateExpectedCompleteTransfer( raster );

		await KittyGraphicsOutputTransaction.WriteAsync(
			session,
			raster,
			cancellation.Token
		);

		Assert.True( cancellation.IsCancellationRequested );
		Assert.Equal( expected, output.GetCombinedWrites() );
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal( 1, output.FlushCount );
		Assert.True(
			output.GetWrite( 0 ).AsSpan().StartsWith(
				Encoding.ASCII.GetBytes( "\u001b_Ga=T," )
			)
		);
		Assert.True(
			output.GetWrite( 1 ).AsSpan().StartsWith(
				Encoding.ASCII.GetBytes( "\u001b_Gm=0,q=2;" )
			)
		);
	}

	[Fact]
	public async Task EveryChunkIsACompleteApcFrame() {
		ScriptedOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		KittyRasterData raster = CreateTwoChunkRaster();

		await KittyGraphicsOutputTransaction.WriteAsync(
			session,
			raster
		);

		Assert.Equal( 2, output.WriteCount );
		foreach ( byte[] write in output.Writes ) {
			Assert.Equal( (byte)0x1B, write[ 0 ] );
			Assert.Equal( (byte)'_', write[ 1 ] );
			Assert.Equal( (byte)0x1B, write[ ^2 ] );
			Assert.Equal( (byte)'\\', write[ ^1 ] );

			TerminalControlFrameStructure structure = TerminalControlFrameStructure.Parse(
				new TerminalResponseFrame(
					TerminalResponseFrameKind.Apc,
					write
				)
			);
			Assert.Equal( TerminalControlFamily.Apc, structure.Family );
			Assert.Equal(
				TerminalStringTerminatorKind.SevenBitSt,
				structure.TerminatorKind
			);
		}
	}

	[Fact]
	public async Task OrdinarySessionOutputCannotInterleaveBetweenCommittedFrames() {
		ScriptedOutput output = new() {
			PauseAfterWriteAttempt = 1
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		KittyRasterData raster = CreateTwoChunkRaster();
		byte[] expectedTransfer = CreateExpectedCompleteTransfer( raster );

		Task transaction = KittyGraphicsOutputTransaction.WriteAsync(
			session,
			raster
		).AsTask();
		await output.WaitUntilPausedAsync();

		Task textWrite = session.WriteTextAsync( "X" ).AsTask();
		await YieldSeveralTimesAsync();
		Assert.False( textWrite.IsCompleted );

		output.ReleasePause();
		await transaction;
		await textWrite;

		Assert.Equal(
			expectedTransfer.Concat( Encoding.UTF8.GetBytes( "X" ) ).ToArray(),
			output.GetCombinedWrites()
		);
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task TransportFailureStopsWithoutRetryOrSpeculativeContinuation() {
		ScriptedOutput output = new() {
			FailOnWriteAttempt = 2
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		KittyRasterData raster = CreateTwoChunkRaster();

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => KittyGraphicsOutputTransaction.WriteAsync(
				session,
				raster
			).AsTask()
		);

		Assert.Equal( "scripted write failure", exception.Message );
		Assert.Equal( 2, output.WriteAttemptCount );
		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
	}

	[Fact]
	public async Task SmallTransactionMatchesApcWriterComposition() {
		ScriptedOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		KittyRasterData raster = CreateSmallRaster();
		byte[] expected = CreateExpectedCompleteTransfer( raster );

		await KittyGraphicsOutputTransaction.WriteAsync(
			session,
			raster
		);

		Assert.Equal( expected, output.GetCombinedWrites() );
		Assert.Equal( 1, output.WriteCount );
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task DisposalWaitsForCommittedKittyTransferBeforeRestorationFlush() {
		ScriptedOutput output = new() {
			PauseAfterWriteAttempt = 1
		};
		TerminalSession session = await OpenSessionAsync( output );
		KittyRasterData raster = CreateTwoChunkRaster();

		Task transaction = KittyGraphicsOutputTransaction.WriteAsync(
			session,
			raster
		).AsTask();
		await output.WaitUntilPausedAsync();

		Task disposal = session.DisposeAsync().AsTask();
		await YieldSeveralTimesAsync();
		Assert.False( disposal.IsCompleted );
		Assert.Equal( 0, output.FlushCount );

		output.ReleasePause();
		await transaction;
		await disposal;

		Assert.Equal( 2, output.FlushCount );
	}

	private static KittyRasterData CreateSmallRaster() {
		return KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1,
				1,
				[ 0, 0, 0 ]
			)
		);
	}

	private static KittyRasterData CreateTwoChunkRaster() {
		byte[] pixels = Enumerable.Range(
			0,
			KittyGraphicsDirectEncoder.MaximumRawChunkBytes + 3
		).Select( index => unchecked( (byte)( index * 13 ) ) ).ToArray();
		return KittyRasterAdapter.Adapt(
			TerminalRasterImage.CreateRgb24(
				1025,
				1,
				pixels
			)
		);
	}

	private static byte[] CreateExpectedCompleteTransfer(
		KittyRasterData raster
	) {
		ArgumentNullException.ThrowIfNull( raster );
		List<byte> result = [];
		foreach ( ReadOnlyMemory<byte> payload in KittyGraphicsDirectEncoder.EncodeDisplayPayloads( raster ) ) {
			result.AddRange( ApcWriter.EncodeFrame( payload.Span ) );
		}
		return result.ToArray();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new SilentInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private static async Task YieldSeveralTimesAsync() {
		for ( int count = 0; count < 64; count++ ) {
			await Task.Yield();
		}
	}

	private sealed class SilentInput : ITerminalInput {
		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			if ( buffer.IsEmpty ) {
				throw new ArgumentException(
					"The scripted input requires a non-empty read buffer.",
					nameof( buffer )
				);
			}
			cancellationToken.ThrowIfCancellationRequested();
			await Task.Delay(
				Timeout.InfiniteTimeSpan,
				cancellationToken
			).ConfigureAwait( false );
			return 0;
		}
	}

	private sealed class ScriptedOutput : ITerminalOutput {
		private readonly object sync = new();
		private readonly List<byte[]> writes = [];
		private readonly TaskCompletionSource paused = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private readonly TaskCompletionSource releasePause = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

		private int writeAttemptCount;
		private int flushCount;

		internal int PauseAfterWriteAttempt {
			get;
			init;
		}

		internal int FailOnWriteAttempt {
			get;
			init;
		}

		internal Action<int>? AfterSuccessfulWrite {
			get;
			init;
		}

		internal int WriteAttemptCount {
			get {
				return Volatile.Read( ref this.writeAttemptCount );
			}
		}

		internal int FlushCount {
			get {
				return Volatile.Read( ref this.flushCount );
			}
		}

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
			}
		}

		internal byte[][] Writes {
			get {
				lock ( this.sync ) {
					return this.writes
						.Select( write => write.ToArray() )
						.ToArray();
				}
			}
		}

		public async ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			int attempt = Interlocked.Increment( ref this.writeAttemptCount );
			if ( this.FailOnWriteAttempt == attempt ) {
				throw new IOException( "scripted write failure" );
			}

			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.AfterSuccessfulWrite?.Invoke( attempt );

			if ( this.PauseAfterWriteAttempt == attempt ) {
				this.paused.TrySetResult();
				await this.releasePause.Task.WaitAsync(
					cancellationToken
				).ConfigureAwait( false );
			}
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			Interlocked.Increment( ref this.flushCount );
			return ValueTask.CompletedTask;
		}

		internal Task WaitUntilPausedAsync() {
			if ( 0 >= this.PauseAfterWriteAttempt ) {
				throw new InvalidOperationException(
					"This scripted output does not have a pause point configured."
				);
			}
			return this.paused.Task;
		}

		internal void ReleasePause() {
			this.releasePause.TrySetResult();
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
				return this.writes
					.SelectMany( write => write )
					.ToArray();
			}
		}
	}

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
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
