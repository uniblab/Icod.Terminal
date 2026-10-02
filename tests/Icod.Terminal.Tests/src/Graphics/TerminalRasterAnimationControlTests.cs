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
/// Freezes T164 per-frame timing and explicit current-frame selection behavior.
/// </summary>
public sealed class TerminalRasterAnimationControlTests {
	[Fact]
	public async Task RootFrameDurationUsesAcknowledgedAnimationControl() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);

		Task<TerminalControlMutationResult> control = resource.Animation
			.SetFrameDurationAsync(
				resource.Animation.RootFrame,
				TimeSpan.FromMilliseconds( 55 )
			)
			.AsTask();
		await YieldSeveralTimesAsync();
		Assert.False( control.IsCompleted );
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,r=1,z=55\u001b\\" ),
			transport.Writes[ 1 ]
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		TerminalControlMutationResult result = await control;
		Assert.True( result.Succeeded );
	}

	[Fact]
	public async Task AppendedFrameSupportsDurationAndCurrentSelectionByOpaqueToken() {
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

		Task<TerminalControlMutationResult> duration = resource.Animation
			.SetFrameDurationAsync(
				frame,
				TimeSpan.FromMilliseconds( 75 )
			)
			.AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,r=2,z=75\u001b\\" ),
			transport.Writes[ 2 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await duration ).Succeeded );

		Task<TerminalControlMutationResult> selection = resource.Animation
			.SelectFrameAsync( frame )
			.AsTask();
		await transport.WaitForWriteCountAsync( 4 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,c=2\u001b\\" ),
			transport.Writes[ 3 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await selection ).Succeeded );
	}

	[Fact]
	public async Task CompositionOfKnownFramesWaitsForAcknowledgementAndUsesOpaqueTokens() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);

		Task<TerminalControlMutationResult> composition = resource.Animation
			.ComposeFrameAsync(
				resource.Animation.RootFrame,
				destination,
				new TerminalRasterSourceRectangle( 0, 0, 1, 1 ),
				destinationX: 0,
				destinationY: 0,
				TerminalRasterFrameCompositionMode.Replace
			).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Assert.False( composition.IsCompleted );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=c,i=77,r=1,c=2,w=1,h=1,X=0,Y=0,x=0,y=0,C=1\u001b\\"
			),
			transport.Writes[ 2 ]
		);
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await composition ).Succeeded );
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
	}

	[Fact]
	public async Task CompositionRejectsForeignAndOutOfBoundsFramesBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource first = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		await using TerminalRasterResource second = await CreateResourceAsync(
			session, transport, imageId: 88u, expectedWriteCount: 2
		);
		int writesBefore = transport.Writes.Count;
		TerminalRasterSourceRectangle pixel = new( 0, 0, 1, 1 );

		Assert.Throws<ArgumentException>(
			() => first.Animation.ComposeFrameAsync(
				first.Animation.RootFrame,
				second.Animation.RootFrame,
				pixel, 0, 0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => first.Animation.ComposeFrameAsync(
				first.Animation.RootFrame,
				first.Animation.RootFrame,
				pixel, 1, 0
			)
		);
		Assert.Throws<ArgumentException>(
			() => first.Animation.ComposeFrameAsync(
				first.Animation.RootFrame,
				first.Animation.RootFrame,
				pixel, 0, 0
			)
		);
		Assert.Equal( writesBefore, transport.Writes.Count );
	}

	[Fact]
	public async Task CompositionRejectionPreservesFramesButMissingIdentityInvalidatesResource() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		TerminalRasterSourceRectangle pixel = new( 0, 0, 1, 1 );

		Task<TerminalControlMutationResult> invalid = resource.Animation
			.ComposeFrameAsync( resource.Animation.RootFrame, destination, pixel, 0, 0 )
			.AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;EINVAL:invalid frame\u001b\\" )
		);
		Assert.Equal( TerminalControlStatus.Failed, ( await invalid ).Status );
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );

		Task<TerminalControlMutationResult> missing = resource.Animation
			.ComposeFrameAsync( resource.Animation.RootFrame, destination, pixel, 0, 0 )
			.AsTask();
		await transport.WaitForWriteCountAsync( 4 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;ENOENT:missing frame\u001b\\" )
		);
		Assert.Equal( TerminalControlStatus.Unavailable, ( await missing ).Status );
		Assert.Equal( TerminalRasterOwnershipStatus.Stale, resource.OwnershipState.Status );
	}

	[Fact]
	public async Task SameFrameNonoverlapComposesButOverlapAndSourceOverflowReject() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u, width: 2, height: 2
		);
		TerminalRasterAnimationFrame root = resource.Animation.RootFrame;

		Assert.Throws<ArgumentOutOfRangeException>(
			() => resource.Animation.ComposeFrameAsync(
				root, root,
				new TerminalRasterSourceRectangle( 1, 1, 2, 1 ),
				0, 0
			)
		);
		Assert.Throws<ArgumentException>(
			() => resource.Animation.ComposeFrameAsync(
				root, root,
				new TerminalRasterSourceRectangle( 0, 0, 2, 1 ),
				0, 0
			)
		);

		Task<TerminalControlMutationResult> composition = resource.Animation
			.ComposeFrameAsync(
				root, root,
				new TerminalRasterSourceRectangle( 0, 0, 1, 1 ),
				1, 1
			).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=c,i=77,r=1,c=1,w=1,h=1,X=0,Y=0,x=1,y=1\u001b\\"
			),
			transport.Writes[ 1 ]
		);
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await composition ).Succeeded );
	}

	[Fact]
	public async Task CompositionCancellationBeforeOutputAndDisposedResourceDoNotWrite() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimation animation = resource.Animation;
		TerminalRasterAnimationFrame root = animation.RootFrame;
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		TerminalRasterSourceRectangle pixel = new( 0, 0, 1, 1 );
		using CancellationTokenSource cancelled = new();
		cancelled.Cancel();
		Assert.Throws<OperationCanceledException>(
			() => animation.ComposeFrameAsync(
				root, destination, pixel, 0, 0,
				cancellationToken: cancelled.Token
			)
		);
		Assert.Equal( 2, transport.Writes.Count );

		await resource.DisposeAsync();
		int writesAfterDisposal = transport.Writes.Count;
		TerminalControlMutationResult unavailable = await animation.ComposeFrameAsync(
			root, destination, pixel, 0, 0
		);
		Assert.Equal( TerminalControlStatus.Unavailable, unavailable.Status );
		Assert.Equal( writesAfterDisposal, transport.Writes.Count );
	}

	[Fact]
	public async Task ConcurrentCompositionSerializesAcknowledgements() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		TerminalRasterSourceRectangle pixel = new( 0, 0, 1, 1 );
		Task<TerminalControlMutationResult> first = resource.Animation.ComposeFrameAsync(
			resource.Animation.RootFrame, destination, pixel, 0, 0
		).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Task<TerminalControlMutationResult> second = resource.Animation.ComposeFrameAsync(
			resource.Animation.RootFrame, destination, pixel, 0, 0
		).AsTask();
		await YieldSeveralTimesAsync();
		Assert.Equal( 3, transport.Writes.Count );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await first ).Succeeded );
		await transport.WaitForWriteCountAsync( 4 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await second ).Succeeded );
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
	}

	[Fact]
	public async Task ForeignAnimationFrameRejectsBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource first = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		await using TerminalRasterResource second = await CreateResourceAsync(
			session,
			transport,
			imageId: 88u,
			expectedWriteCount: 2
		);
		int writesBefore = transport.Writes.Count;

		Assert.Throws<ArgumentException>(
			() => first.Animation.SelectFrameAsync(
				second.Animation.RootFrame
			)
		);
		Assert.Throws<ArgumentException>(
			() => first.Animation.SetFrameDurationAsync(
				second.Animation.RootFrame,
				TimeSpan.FromMilliseconds( 40 )
			)
		);
		Assert.Equal( writesBefore, transport.Writes.Count );
	}

	[Fact]
	public async Task ControlledFrameFailureDoesNotPoisonKnownFrameSequence() {
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

		Task<TerminalControlMutationResult> failed = resource.Animation
			.SelectFrameAsync( frame )
			.AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;EINVAL:invalid frame\u001b\\" )
		);
		TerminalControlMutationResult failure = await failed;
		Assert.Equal( TerminalControlStatus.Failed, failure.Status );
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Current,
				TerminalRasterAnimationLossReason.None
			),
			resource.Animation.State
		);

		Task<TerminalControlMutationResult> retry = resource.Animation
			.SetFrameDurationAsync(
				frame,
				TimeSpan.FromMilliseconds( 90 )
			)
			.AsTask();
		await transport.WaitForWriteCountAsync( 4 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		Assert.True( ( await retry ).Succeeded );
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
		uint imageId,
		int expectedWriteCount = 1,
		int width = 1,
		int height = 1
	) {
		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync(
				TerminalRasterImage.CreateRgb24(
					width,
					height,
					new byte[checked( width * height * 3 )]
				)
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},I={expectedWriteCount};OK\u001b\\"
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
}
