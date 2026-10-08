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
/// Freezes the optional-response contract for Kitty frame composition.
/// </summary>
public sealed class TerminalRasterAnimationOptionalCompositionTests {
	[Fact]
	public async Task CorrelatedSuccessReportsProtocolAcknowledgementAndEvidence() {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, clock );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport
		);

		Task<TerminalControlMutationResult> composition = StartComposition( resource );
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish( Response( "OK" ) );

		TerminalControlMutationResult result = await composition;
		Assert.True( result.Succeeded );
		Assert.Equal(
			TerminalControlMutationConfirmation.ProtocolAcknowledged,
			result.Confirmation
		);
		Assert.Equal(
			TerminalCapabilitySupport.Verified,
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameComposition
			).Support
		);
	}

	[Fact]
	public async Task CommittedSilenceReportsOutputCommitmentWithoutEvidence() {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, clock );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport
		);

		Task<TerminalControlMutationResult> composition = StartComposition( resource );
		await transport.WaitForWriteCountAsync( 2 );
		clock.Advance( TimeSpan.FromSeconds( 2 ) );
		await YieldSeveralTimesAsync();

		TerminalControlMutationResult result = await composition;
		Assert.True( result.Succeeded );
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			result.Confirmation
		);
		AssertOperationUnknown( session );
	}

	[Theory]
	[InlineData( "EINVAL:invalid composition", TerminalControlStatus.Failed )]
	[InlineData( "ENOSPC:frame storage", TerminalControlStatus.Failed )]
	[InlineData( "EIO:synthetic failure", TerminalControlStatus.Failed )]
	[InlineData( "ENOENT:missing image", TerminalControlStatus.Unavailable )]
	public async Task CorrelatedFailuresRemainDefinitive(
		string response,
		TerminalControlStatus expectedStatus
	) {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, clock );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport
		);

		Task<TerminalControlMutationResult> composition = StartComposition( resource );
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish( Response( response ) );

		TerminalControlMutationResult result = await composition;
		Assert.Equal( expectedStatus, result.Status );
		Assert.Equal(
			TerminalControlMutationConfirmation.Unspecified,
			result.Confirmation
		);
		AssertOperationUnknown( session );
	}

	[Fact]
	public async Task UnrelatedRepliesCannotCompleteComposition() {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, clock );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport
		);

		Task<TerminalControlMutationResult> composition = StartComposition( resource );
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=88;OK\u001b\\" ) );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=1;OK\u001b\\" ) );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_GI=77;OK\u001b\\" ) );
		await YieldSeveralTimesAsync();
		Assert.False( composition.IsCompleted );

		transport.Publish( Response( "OK" ) );
		Assert.Equal(
			TerminalControlMutationConfirmation.ProtocolAcknowledged,
			( await composition ).Confirmation
		);
	}

	[Fact]
	public async Task TimeoutBeforeFirstWriteRemainsATimeoutAndEmitsNothing() {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, clock );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport
		);
		IDisposable outputLease = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);
		try {
			Task<TerminalControlMutationResult> composition = StartComposition( resource );
			await YieldSeveralTimesAsync();
			clock.Advance( TimeSpan.FromSeconds( 2 ) );
			await Assert.ThrowsAsync<TimeoutException>( () => composition );
			Assert.Single( transport.Writes );
		} finally {
			outputLease.Dispose();
		}
	}

	[Fact]
	public async Task TimeoutAfterWriteButBeforeFlushRemainsATimeout() {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, clock );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport
		);
		transport.BlockNextFlush = true;

		Task<TerminalControlMutationResult> composition = StartComposition( resource );
		await transport.WaitForWriteCountAsync( 2 );
		await transport.WaitForBlockedFlushAsync();
		clock.Advance( TimeSpan.FromSeconds( 2 ) );
		await Assert.ThrowsAsync<TimeoutException>( () => composition );
		transport.ReleaseBlockedFlush();
	}

	[Fact]
	public async Task LateReplyRemainsOwnedAndCannotCompleteNextComposition() {
		ManualMonotonicClock clock = new();
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport, clock );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport
		);

		Task<TerminalControlMutationResult> first = StartComposition( resource );
		await transport.WaitForWriteCountAsync( 2 );
		clock.Advance( TimeSpan.FromSeconds( 1 ) );
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			( await first ).Confirmation
		);

		Task<TerminalControlMutationResult> second = StartComposition( resource );
		await YieldSeveralTimesAsync();
		Assert.Equal( 2, transport.Writes.Count );
		transport.Publish( Response( "OK" ) );
		await transport.WaitForWriteCountAsync( 3 );
		Assert.False( second.IsCompleted );
		AssertOperationUnknown( session );

		transport.Publish( Response( "OK" ) );
		Assert.Equal(
			TerminalControlMutationConfirmation.ProtocolAcknowledged,
			( await second ).Confirmation
		);
	}

	private static Task<TerminalControlMutationResult> StartComposition(
		TerminalRasterResource resource
	) {
		return resource.Animation.ComposeFrameAsync(
			resource.Animation.RootFrame,
			resource.Animation.RootFrame,
			new TerminalRasterSourceRectangle( 0, 0, 1, 1 ),
			1,
			1
		).AsTask();
	}

	private static byte[] Response(
		string message
	) {
		return Encoding.ASCII.GetBytes( $"\u001b_Gi=77;{message}\u001b\\" );
	}

	private static void AssertOperationUnknown(
		TerminalSession session
	) {
		TerminalRasterOperationStatus status = session.InspectRasterOperation(
			TerminalRasterOperation.FrameComposition
		);
		Assert.Equal( TerminalCapabilitySupport.Unknown, status.Support );
		Assert.Equal( TerminalCapabilityEvidenceKind.None, status.EvidenceKind );
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport
	) {
		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync(
				TerminalRasterImage.CreateRgb24( 2, 2, new byte[12] )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,I=1;OK\u001b\\" )
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
		session.RecordSemanticOperationEvidence(
			TerminalSemanticOperation.PersistentRasterGraphics,
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
		private readonly TaskCompletionSource blockedFlushStarted = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private readonly TaskCompletionSource blockedFlushRelease = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

		internal bool BlockNextFlush {
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
			lock ( this.synchronization ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
			return ValueTask.CompletedTask;
		}

		public async ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.BlockNextFlush ) {
				this.blockedFlushStarted.TrySetResult();
				await this.blockedFlushRelease.Task.ConfigureAwait( false );
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel is closed."
				);
			}
		}

		internal void ReleaseBlockedFlush() {
			this.blockedFlushRelease.TrySetResult();
		}

		internal async Task WaitForBlockedFlushAsync() {
			using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
			await this.blockedFlushStarted.Task.WaitAsync( timeout.Token );
		}

		internal async Task WaitForWriteCountAsync(
			int expected
		) {
			using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
			while ( this.Writes.Count < expected ) {
				await this.writeSignal.WaitAsync( timeout.Token ).ConfigureAwait( false );
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
			return TimeSpan.FromTicks( endingTimestamp - startingTimestamp );
		}

		public ValueTask DelayAsync(
			TimeSpan delay,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( TimeSpan.Zero == delay ) {
				return ValueTask.CompletedTask;
			}
			return new ValueTask( this.DelayCoreAsync( delay, cancellationToken ) );
		}

		internal void Advance(
			TimeSpan elapsed
		) {
			List<DelayWaiter> due;
			lock ( this.synchronization ) {
				this.timestamp = checked( this.timestamp + elapsed.Ticks );
				due = this.waiters.Where(
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
				waiter = new DelayWaiter( checked( this.timestamp + delay.Ticks ) );
				this.waiters.Add( waiter );
			}
			using CancellationTokenRegistration registration = cancellationToken.Register(
				static state => {
					var tuple = (Tuple<TaskCompletionSource, CancellationToken>)state!;
					tuple.Item1.TrySetCanceled( tuple.Item2 );
				},
				Tuple.Create( waiter.Completion, cancellationToken )
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
				this.Completion = new TaskCompletionSource(
					TaskCreationOptions.RunContinuationsAsynchronously
				);
			}

			internal long DueTimestamp {
				get;
			}

			internal TaskCompletionSource Completion {
				get;
			}
		}
	}
}
