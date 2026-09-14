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

using System.IO;
using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Timing;
using Xunit;

/// <summary>
/// Hardens T156 placeholder acknowledgement timeout, late-response, and transport semantics.
/// </summary>
public sealed class TerminalRasterPlaceholderTransactionHardeningTests {
	[Fact]
	public async Task TimedOutPlaceholderCreationDoesNotAcceptLateResponseForLaterPlaceholder() {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);

		Task<TerminalControlResult<TerminalRasterPlaceholder>> first =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		await YieldSeveralTimesAsync();
		clock.Advance( TimeSpan.FromSeconds( 2 ) );

		await Assert.ThrowsAsync<TimeoutException>( () => first );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			resource.OwnershipState
		);

		Task<TerminalControlResult<TerminalRasterPlaceholder>> second =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( 3 );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=1;OK\u001b\\" )
		);
		await YieldSeveralTimesAsync();
		Assert.False( second.IsCompleted );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=2;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterPlaceholder> result = await second;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		TerminalRasterPlaceholder placeholder = Assert.IsType<TerminalRasterPlaceholder>(
			result.Value
		);
		await placeholder.DisposeAsync();
	}

	[Fact]
	public async Task PlaceholderWriteFailureRollsBackReservationWithoutStalingResource() {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		transport.FailNextWrite = true;

		await Assert.ThrowsAsync<IOException>(
			() => resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			).AsTask()
		);

		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			resource.OwnershipState
		);
		Assert.Equal( 1, transport.Writes.Count );

		Task<TerminalControlResult<TerminalRasterPlaceholder>> retry =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=2;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterPlaceholder> result = await retry;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		TerminalRasterPlaceholder placeholder = Assert.IsType<TerminalRasterPlaceholder>(
			result.Value
		);
		await placeholder.DisposeAsync();
	}

	[Fact]
	public async Task PlaceholderFlushFailureAfterCommittedFrameDoesNotInventOwnershipLoss() {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			clock
		);
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		transport.FailNextFlush = true;

		await Assert.ThrowsAsync<IOException>(
			() => resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			).AsTask()
		);

		Assert.Equal( 2, transport.Writes.Count );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=77,p=1,C=1,U=1,c=1,r=1\u001b\\"
			),
			transport.Writes[ 1 ]
		);
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			resource.OwnershipState
		);

		Task<TerminalControlResult<TerminalRasterPlaceholder>> retry =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=2;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterPlaceholder> result = await retry;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		TerminalRasterPlaceholder placeholder = Assert.IsType<TerminalRasterPlaceholder>(
			result.Value
		);
		await placeholder.DisposeAsync();
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId
	) {
		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync(
				TerminalRasterImage.CreateRgb24(
					1,
					1,
					[ 1, 2, 3 ]
				)
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},I=1;OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport,
		IMonotonicClock clock
	) {
		TerminalSession session = await TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				MonotonicClock = clock,
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

	private static async Task YieldSeveralTimesAsync() {
		for ( int iteration = 0; iteration < 8; ++iteration ) {
			await Task.Yield();
		}
	}

	private sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly object synchronization = new();
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private readonly List<byte[]> writes = [];

		internal bool FailNextWrite {
			get;
			set;
		}

		internal bool FailNextFlush {
			get;
			set;
		}

		internal IReadOnlyList<byte[]> Writes {
			get {
				lock ( this.synchronization ) {
					return this.writes.Select(
						static value => value.ToArray()
					).ToArray();
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
			if ( this.FailNextWrite ) {
				this.FailNextWrite = false;
				throw new IOException( "Synthetic placeholder write failure." );
			}

			lock ( this.synchronization ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.FailNextFlush ) {
				this.FailNextFlush = false;
				throw new IOException( "Synthetic placeholder flush failure." );
			}
			return ValueTask.CompletedTask;
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
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new(
				TimeSpan.FromSeconds( 5 )
			);
			while ( this.Writes.Count < expected ) {
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}
	}

	private sealed class ManualMonotonicClock : IMonotonicClock {
		private readonly object synchronization = new();
		private readonly List<DelayWaiter> waiters = [];
		private long timestamp;

		public long GetTimestamp() {
			lock ( this.synchronization ) {
				return this.timestamp;
			}
		}

		public TimeSpan GetElapsedTime(
			long startingTimestamp,
			long endingTimestamp
		) {
			return TimeSpan.FromTicks(
				endingTimestamp - startingTimestamp
			);
		}

		public ValueTask DelayAsync(
			TimeSpan delay,
			CancellationToken cancellationToken = default
		) {
			if ( TimeSpan.Zero > delay ) {
				throw new ArgumentOutOfRangeException( nameof( delay ) );
			}
			cancellationToken.ThrowIfCancellationRequested();
			if ( TimeSpan.Zero == delay ) {
				return ValueTask.CompletedTask;
			}

			return new ValueTask(
				this.DelayCoreAsync(
					delay,
					cancellationToken
				)
			);
		}

		internal void Advance(
			TimeSpan elapsed
		) {
			if ( TimeSpan.Zero > elapsed ) {
				throw new ArgumentOutOfRangeException( nameof( elapsed ) );
			}

			List<DelayWaiter> due;
			lock ( this.synchronization ) {
				this.timestamp = checked(
					this.timestamp + elapsed.Ticks
				);
				due = this.waiters
					.Where(
						waiter => waiter.DueTimestamp <= this.timestamp
					).ToList();
			}

			foreach ( DelayWaiter waiter in due ) {
				waiter.Completion.TrySetResult();
			}
		}

		private async Task DelayCoreAsync(
			TimeSpan delay,
			CancellationToken cancellationToken
		) {
			DelayWaiter waiter;
			lock ( this.synchronization ) {
				waiter = new DelayWaiter(
					checked( this.timestamp + delay.Ticks )
				);
				this.waiters.Add( waiter );
			}

			using CancellationTokenRegistration registration = cancellationToken.Register(
				static state => {
					var tuple = (Tuple<TaskCompletionSource, CancellationToken>)state!;
					tuple.Item1.TrySetCanceled( tuple.Item2 );
				},
				Tuple.Create(
					waiter.Completion,
					cancellationToken
				)
			);

			try {
				await waiter.Completion.Task.ConfigureAwait( false );
			} finally {
				lock ( this.synchronization ) {
					this.waiters.Remove( waiter );
				}
			}
		}

		private sealed class DelayWaiter {
			internal DelayWaiter(
				long dueTimestamp
			) {
				this.DueTimestamp = dueTimestamp;
			}

			internal long DueTimestamp {
				get;
			}

			internal TaskCompletionSource Completion {
				get;
			} = new(
				TaskCreationOptions.RunContinuationsAsynchronously
			);
		}
	}
}
