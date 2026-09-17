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
		int expectedWriteCount = 1
	) {
		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync(
				TerminalRasterImage.CreateRgb24(
					1,
					1,
					[ 1, 2, 3 ]
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
