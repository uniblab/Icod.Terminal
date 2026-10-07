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
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Qualifies fixed-count T167 integration races for persistent-raster animation.
/// </summary>
public sealed class TerminalRasterAnimationIntegrationHardeningTests {
	private const int LoadingAppendCount = 8;

	[Fact]
	public async Task ConcurrentPlaybackControlsAreSerializedWithoutResponseWaiters() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);

		Task<TerminalControlMutationResult> loading = resource.Animation
			.RunLoadingAsync()
			.AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Task<TerminalControlMutationResult> stop = resource.Animation
			.StopAsync()
			.AsTask();
		await transport.WaitForWriteCountAsync( 3 );

		Assert.Equal( 3, transport.Writes.Count );
		Assert.Equal(
			"\u001b_Ga=a,i=77,s=2,q=2\u001b\\",
			Encoding.ASCII.GetString( transport.Writes[ 1 ] )
		);

		Task<TerminalControlMutationResult[]> controls = Task.WhenAll( loading, stop );
		TerminalControlMutationResult[] results = await AwaitWithoutResponseAsync(
			controls
		);
		Assert.Equal(
			"\u001b_Ga=a,i=77,s=1,q=2\u001b\\",
			Encoding.ASCII.GetString( transport.Writes[ 2 ] )
		);
		Assert.All(
			results,
			static result => Assert.Equal(
				TerminalControlMutationConfirmation.OutputCommitted,
				result.Confirmation
			)
		);
	}

	[Fact]
	public async Task LoadingModeSupportsBoundedAcknowledgedAppendChurn() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);

		Task<TerminalControlMutationResult> loading = resource.Animation
			.RunLoadingAsync()
			.AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			( await AwaitWithoutResponseAsync( loading ) ).Confirmation
		);

		for ( int index = 0; index < LoadingAppendCount; ++index ) {
			Task<TerminalControlResult<TerminalRasterAnimationFrame>> append =
				resource.Animation.AddFrameAsync(
					CreateFrameImage( index ),
					TimeSpan.FromMilliseconds( 20 + index )
				).AsTask();
			await transport.WaitForWriteCountAsync( 3 + index );
			transport.Publish( OkResponse() );
			TerminalControlResult<TerminalRasterAnimationFrame> result = await append;
			Assert.Equal( TerminalControlStatus.Available, result.Status );
			Assert.Equal(
				2 + index,
				Assert.IsType<TerminalRasterAnimationFrame>( result.Value ).SequenceNumber
			);
		}

		Task<TerminalControlMutationResult> stop = resource.Animation.StopAsync().AsTask();
		await transport.WaitForWriteCountAsync( 3 + LoadingAppendCount );
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			( await AwaitWithoutResponseAsync( stop ) ).Confirmation
		);
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Current,
				TerminalRasterAnimationLossReason.None
			),
			resource.Animation.State
		);
	}

	[Fact]
	public async Task ResourceDisposalWhilePlaybackWaitsForOutputGateEmitsNoControl() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);
		IDisposable outputLease = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		Task<TerminalControlMutationResult> run = resource.Animation.RunAsync().AsTask();
		await YieldSeveralTimesAsync();
		Assert.False( run.IsCompleted );
		Assert.Single( transport.Writes );
		Task disposal = resource.DisposeAsync().AsTask();
		await YieldSeveralTimesAsync();
		Assert.False( disposal.IsCompleted );
		outputLease.Dispose();
		await transport.WaitForWriteCountAsync( 2 );
		if ( transport.Writes.Any(
			static value => Encoding.ASCII.GetString( value ).Contains(
				"Ga=a,",
				StringComparison.Ordinal
			)
		) ) {
			transport.Publish( OkResponse() );
		}

		TerminalControlMutationResult result = await run;
		await disposal;

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal(
			TerminalControlMutationConfirmation.Unspecified,
			result.Confirmation
		);
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.OwnerDisposed,
				TerminalRasterAnimationLossReason.ExplicitResourceDisposal
			),
			resource.Animation.State
		);
		Assert.DoesNotContain(
			transport.Writes,
			static value => Encoding.ASCII.GetString( value ).Contains(
				"Ga=a,",
				StringComparison.Ordinal
			)
		);
		Assert.Equal(
			TerminalRasterAnimationStatus.OwnerDisposed,
			resource.Animation.State.Status
		);
	}

	[Fact]
	public async Task DisposedFrameTokenThrowsWithoutFurtherOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);
		// Reply at the flush boundary so runner scheduling cannot consume the
		// one-second protocol deadline between the write signal and test continuation.
		transport.ReplyToNextFlush = true;
		TerminalControlResult<TerminalRasterAnimationFrame> append = await resource.Animation
			.AddFrameAsync( CreateFrameImage( 0 ), TimeSpan.FromMilliseconds( 40 ) );
		Assert.Equal( TerminalControlStatus.Available, append.Status );
		TerminalRasterAnimationFrame frame = Assert.IsType<TerminalRasterAnimationFrame>( append.Value );
		Assert.Equal( 2, transport.Writes.Count );

		await resource.DisposeAsync();
		int writesBeforeRejectedControls = transport.Writes.Count;
		Assert.Throws<ObjectDisposedException>(
			() => resource.Animation.SetFrameDurationAsync(
				frame,
				TimeSpan.FromMilliseconds( 25 )
			)
		);
		Assert.Throws<ObjectDisposedException>(
			() => resource.Animation.SelectFrameAsync( frame )
		);
		Assert.Equal( writesBeforeRejectedControls, transport.Writes.Count );
	}

	private static TerminalRasterImage CreateFrameImage(
		int seed
	) {
		return TerminalRasterImage.CreateRgb24(
			1,
			1,
			[
				unchecked( (byte)( seed + 4 ) ),
				unchecked( (byte)( seed + 5 ) ),
				unchecked( (byte)( seed + 6 ) )
			]
		);
	}

	private static async Task<TerminalRasterAnimationFrame> AppendFrameAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		int expectedWriteCount
	) {
		Task<TerminalControlResult<TerminalRasterAnimationFrame>> append =
			resource.Animation.AddFrameAsync(
				CreateFrameImage( 0 ),
				TimeSpan.FromMilliseconds( 40 )
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish( OkResponse() );
		TerminalControlResult<TerminalRasterAnimationFrame> result = await append;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterAnimationFrame>( result.Value );
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport
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
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,I=1;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport
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
				MonotonicClock = new FrozenMonotonicClock(),
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

	private static byte[] OkResponse() {
		return Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" );
	}

	private static async Task YieldSeveralTimesAsync() {
		for ( int iteration = 0; iteration < 8; ++iteration ) {
			await Task.Yield();
		}
	}

	private static async Task<T> AwaitWithoutResponseAsync<T>(
		Task<T> task
	) {
		Task completed = await Task.WhenAny(
			task,
			Task.Delay( TimeSpan.FromMilliseconds( 250 ) )
		);
		Assert.Same( task, completed );
		return await task;
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

		internal bool ReplyToNextFlush {
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

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.ReplyToNextFlush ) {
				this.ReplyToNextFlush = false;
				this.Publish( OkResponse() );
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
}
