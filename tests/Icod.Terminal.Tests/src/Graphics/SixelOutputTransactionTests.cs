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
/// Verifies D176 committed, serialized, streaming Sixel output semantics.
/// </summary>
public sealed class SixelOutputTransactionTests {
	[Fact]
	public async Task PreCancelledTransactionWritesNothing() {
		ScriptedOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		SixelPaletteImage image = CreateSmallImage();
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => SixelOutputTransaction.WriteAsync(
				session,
				image,
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
		SixelPaletteImage image = CreateSmallImage();
		using IDisposable lease = await session.AcquireSessionOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();

		Task transaction = SixelOutputTransaction.WriteAsync(
			session,
			image,
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
	public async Task CancellationAfterPrefixCommitDoesNotTruncateFrame() {
		using CancellationTokenSource cancellation = new();
		ScriptedOutput output = new() {
			AfterSuccessfulWrite = attempt => {
				if ( 1 == attempt ) {
					cancellation.Cancel();
				}
			}
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		SixelPaletteImage image = CreateSmallImage();
		byte[] expected = CreateExpectedCompleteFrame( image );

		await SixelOutputTransaction.WriteAsync(
			session,
			image,
			cancellation.Token
		);

		Assert.True( cancellation.IsCancellationRequested );
		Assert.Equal( expected, output.GetCombinedWrites() );
		Assert.Equal( 1, output.FlushCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001bP0;1;0q" ),
			output.GetWrite( 0 )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b\\" ),
			output.GetWrite( output.WriteCount - 1 )
		);
	}

	[Fact]
	public async Task OrdinarySessionOutputCannotInterleaveInsideCommittedFrame() {
		ScriptedOutput output = new() {
			PauseAfterWriteAttempt = 1
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		SixelPaletteImage image = CreateSmallImage();
		byte[] expectedFrame = CreateExpectedCompleteFrame( image );

		Task transaction = SixelOutputTransaction.WriteAsync(
			session,
			image
		).AsTask();
		await output.WaitUntilPausedAsync();

		Task textWrite = session.WriteTextAsync( "X" ).AsTask();
		await YieldSeveralTimesAsync();
		Assert.False( textWrite.IsCompleted );

		output.ReleasePause();
		await transaction;
		await textWrite;

		byte[] combined = output.GetCombinedWrites();
		Assert.Equal(
			expectedFrame.Concat( Encoding.UTF8.GetBytes( "X" ) ).ToArray(),
			combined
		);
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task TransportFailureIsSurfacedWithoutRetryOrSpeculativeTerminator() {
		ScriptedOutput output = new() {
			FailOnWriteAttempt = 3
		};
		await using TerminalSession session = await OpenSessionAsync( output );
		SixelPaletteImage image = CreateSmallImage();

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => SixelOutputTransaction.WriteAsync(
				session,
				image
			).AsTask()
		);

		Assert.Equal( "scripted write failure", exception.Message );
		Assert.Equal( 3, output.WriteAttemptCount );
		Assert.Equal( 2, output.WriteCount );
		Assert.Equal( 0, output.FlushCount );
		Assert.DoesNotContain(
			output.Writes,
			write => write.SequenceEqual(
				Encoding.ASCII.GetBytes( "\u001b\\" )
			)
		);
	}

	[Fact]
	public async Task SmallStreamingTransactionMatchesCompleteDcsWriterFrame() {
		ScriptedOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		SixelPaletteImage image = CreateSmallImage();
		byte[] expected = CreateExpectedCompleteFrame( image );

		await SixelOutputTransaction.WriteAsync(
			session,
			image
		);

		Assert.Equal( expected, output.GetCombinedWrites() );
		Assert.Equal( 1, output.FlushCount );
	}

	[Fact]
	public async Task DisposalWaitsForCommittedGraphicsBeforeRestorationFlush() {
		ScriptedOutput output = new() {
			PauseAfterWriteAttempt = 1
		};
		TerminalSession session = await OpenSessionAsync( output );
		SixelPaletteImage image = CreateSmallImage();

		Task transaction = SixelOutputTransaction.WriteAsync(
			session,
			image
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

	private static SixelPaletteImage CreateSmallImage() {
		TerminalRasterImage source = TerminalRasterImage.CreateIndexed8(
			2,
			1,
			[ 0, 0 ],
			[
				new TerminalRasterColor( 0, 0, 0 )
			]
		);
		return SixelPaletteQuantizer.Quantize( source );
	}

	private static byte[] CreateExpectedCompleteFrame(
		SixelPaletteImage image
	) {
		ArgumentNullException.ThrowIfNull( image );
		byte[] payload = SixelEncoder.EncodePayloadSegments( image )
			.SelectMany( segment => segment.ToArray() )
			.ToArray();
		return DcsWriter.EncodeFrame(
			SixelCodec.EncodeCanonicalDcsParameters(),
			ReadOnlySpan<byte>.Empty,
			(byte)'q',
			payload
		);
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
