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
/// Freezes T165 terminal-driven persistent-raster animation playback behavior.
/// </summary>
public sealed class TerminalRasterAnimationPlaybackTests {
	[Fact]
	public async Task StopUsesAcknowledgedTerminalDrivenStopControl() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);

		Task<TerminalControlMutationResult> stop = resource.Animation.StopAsync().AsTask();
		await YieldSeveralTimesAsync();
		Assert.False( stop.IsCompleted );
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,s=1\u001b\\" ),
			transport.Writes[ 1 ]
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await stop ).Succeeded );
	}

	[Fact]
	public async Task LoadingRunUsesAcknowledgedTerminalDrivenLoadingControl() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);

		Task<TerminalControlMutationResult> run = resource.Animation.RunLoadingAsync().AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,s=2\u001b\\" ),
			transport.Writes[ 1 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await run ).Succeeded );
	}

	[Fact]
	public async Task NormalRunDefaultsToInfiniteTerminalLooping() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);

		Task<TerminalControlMutationResult> run = resource.Animation.RunAsync().AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,s=3,v=1\u001b\\" ),
			transport.Writes[ 1 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await run ).Succeeded );
	}

	[Theory]
	[InlineData( 1, 2 )]
	[InlineData( 3, 4 )]
	public async Task NormalRunMapsSemanticAdditionalRepeatsToProtocolLoopCount(
		int repeatCount,
		int protocolValue
	) {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);

		Task<TerminalControlMutationResult> run = resource.Animation.RunAsync(
			new TerminalRasterAnimationPlaybackOptions {
				RepeatCount = repeatCount
			}
		).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				$"\u001b_Ga=a,i=77,s=3,v={protocolValue}\u001b\\"
			),
			transport.Writes[ 1 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await run ).Succeeded );
	}

	[Fact]
	public async Task InvalidFiniteRepeatRejectsBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		int writesBefore = transport.Writes.Count;

		Assert.Throws<ArgumentOutOfRangeException>(
			() => resource.Animation.RunAsync(
				new TerminalRasterAnimationPlaybackOptions {
					RepeatCount = 0
				}
			)
		);
		Assert.Equal( writesBefore, transport.Writes.Count );
	}

	[Fact]
	public async Task StopRemainsAvailableButRunModesRejectAfterSequenceUncertainty() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport,
			expectedWriteCount: 2
		);
		TerminalPersistentRasterAnimationFrameState frameState = Assert.IsType<TerminalPersistentRasterAnimationFrameState>(
			frame.State
		);
		Assert.True( frameState.Animation.TryMarkSequenceUncertain() );
		Assert.Equal(
			TerminalRasterAnimationStatus.SequenceUncertain,
			resource.Animation.State.Status
		);

		Task<TerminalControlMutationResult> stop = resource.Animation.StopAsync().AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,s=1\u001b\\" ),
			transport.Writes[ 2 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await stop ).Succeeded );

		int writesBeforeRejectedRuns = transport.Writes.Count;
		TerminalControlMutationResult loading = await resource.Animation.RunLoadingAsync();
		TerminalControlMutationResult normal = await resource.Animation.RunAsync();
		Assert.Equal( TerminalControlStatus.Unavailable, loading.Status );
		Assert.Equal( TerminalControlStatus.Unavailable, normal.Status );
		Assert.Equal( writesBeforeRejectedRuns, transport.Writes.Count );
		Assert.Equal(
			TerminalRasterAnimationStatus.SequenceUncertain,
			resource.Animation.State.Status
		);
	}

	[Fact]
	public async Task ControlledPlaybackFailureDoesNotPoisonFrameSequence() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);

		Task<TerminalControlMutationResult> run = resource.Animation.RunLoadingAsync().AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;EINVAL:invalid animation state\u001b\\" )
		);
		TerminalControlMutationResult failure = await run;
		Assert.Equal( TerminalControlStatus.Failed, failure.Status );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Current,
				TerminalRasterAnimationLossReason.None
			),
			resource.Animation.State
		);
	}

	private static async Task<TerminalRasterAnimationFrame> AppendFrameAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		int expectedWriteCount
	) {
		Task<TerminalControlResult<TerminalRasterAnimationFrame>> append =
			resource.Animation.AddFrameAsync(
				TerminalRasterImage.CreateRgb24(
					1,
					1,
					[ 4, 5, 6 ]
				),
				TimeSpan.FromMilliseconds( 40 )
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterAnimationFrame> result = await append;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterAnimationFrame>( result.Value );
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
