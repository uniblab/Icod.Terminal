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
/// Freezes T166 persistent-raster animation lifecycle propagation.
/// </summary>
public sealed class TerminalRasterAnimationLifecycleTests {
	[Fact]
	public async Task SessionInvalidationStalesPrivateAnimationFrameState() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport
		);

		session.InvalidateState();

		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Stale,
				TerminalRasterAnimationLossReason.SessionStateLost
			),
			resource.Animation.State
		);
		AssertPrivateAnimationState(
			frame,
			TerminalRasterAnimationStatus.Stale,
			TerminalRasterAnimationLossReason.SessionStateLost
		);
	}

	[Fact]
	public async Task PlacementEnoentStalesPrivateAnimationFrameState() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport
		);
		await using TerminalRasterPlacement placement = await CreatePlacementAsync(
			resource,
			transport
		);

		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlMutationResult> update = placement.UpdateAsync().AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=77,p=1;ENOENT:synthetic missing image\u001b\\"
			)
		);
		Assert.Equal(
			TerminalControlStatus.Unavailable,
			( await update ).Status
		);

		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Stale,
				TerminalRasterAnimationLossReason.ResourceMissing
			),
			resource.Animation.State
		);
		AssertPrivateAnimationState(
			frame,
			TerminalRasterAnimationStatus.Stale,
			TerminalRasterAnimationLossReason.ResourceMissing
		);
	}

	[Fact]
	public async Task IntentionalInternalResourceReleaseReleasesPrivateAnimationState() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport
		);

		await session.ReleasePersistentRasterResourceAsync( resource.State );

		AssertPrivateAnimationState(
			frame,
			TerminalRasterAnimationStatus.Released,
			TerminalRasterAnimationLossReason.ResourceReleased
		);
	}

	[Fact]
	public async Task ResourceWrapperDisposalMarksPrivateAnimationOwnerDisposedAndUsesResourceCleanup() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport
		);
		int baselineWrites = transport.Writes.Count;

		await resource.DisposeAsync();

		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.OwnerDisposed,
				TerminalRasterAnimationLossReason.ExplicitResourceDisposal
			),
			resource.Animation.State
		);
		AssertPrivateAnimationState(
			frame,
			TerminalRasterAnimationStatus.OwnerDisposed,
			TerminalRasterAnimationLossReason.ExplicitResourceDisposal
		);
		byte[][] cleanup = transport.Writes.Skip( baselineWrites ).ToArray();
		Assert.Single( cleanup );
		Assert.Equal(
			"\u001b_Ga=d,d=I,i=77,q=2\u001b\\",
			Encoding.ASCII.GetString( cleanup[ 0 ] )
		);
	}

	[Fact]
	public async Task PlacementAndPlaceholderDisposalLeaveAnimationCurrent() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync(
			resource,
			transport
		);
		TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			resource,
			transport,
			placementId: 2u
		);

		await placement.DisposeAsync();
		AssertPrivateAnimationState(
			frame,
			TerminalRasterAnimationStatus.Current,
			TerminalRasterAnimationLossReason.None
		);
		await placeholder.DisposeAsync();
		AssertPrivateAnimationState(
			frame,
			TerminalRasterAnimationStatus.Current,
			TerminalRasterAnimationLossReason.None
		);
	}

	[Fact]
	public async Task SessionTeardownStalesPrivateAnimationFrameState() {
		ScriptedTransport transport = new();
		TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport
		);

		await session.DisposeAsync();

		AssertPrivateAnimationState(
			frame,
			TerminalRasterAnimationStatus.Stale,
			TerminalRasterAnimationLossReason.SessionStateLost
		);
	}

	private static void AssertPrivateAnimationState(
		TerminalRasterAnimationFrame frame,
		TerminalRasterAnimationStatus expectedStatus,
		TerminalRasterAnimationLossReason expectedLossReason
	) {
		TerminalPersistentRasterAnimationFrameState frameState = Assert.IsType<
			TerminalPersistentRasterAnimationFrameState
		>( frame.State );
		Assert.Equal(
			new TerminalRasterAnimationState(
				expectedStatus,
				expectedLossReason
			),
			frameState.Animation.ObserveState()
		);
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
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
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,I=1;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async Task<TerminalRasterAnimationFrame> AppendFrameAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
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

	private static async Task<TerminalRasterPlacement> CreatePlacementAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreatePlacementAsync().AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=1;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterPlacement> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterPlacement>( result.Value );
	}

	private static async Task<TerminalRasterPlaceholder> CreatePlaceholderAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		uint placementId
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlaceholder>> creation =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi=77,p={placementId};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterPlaceholder> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterPlaceholder>( result.Value );
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
