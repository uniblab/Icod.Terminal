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
/// Defines the T134 acknowledged relative persistent-raster placement transaction contract.
/// </summary>
public sealed class TerminalPersistentRasterRelativePlacementIntegrationTests {
	[Fact]
	public async Task RelativeCreationUsesPrivateParentFieldsAndChildAcknowledgement() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource parentResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		TerminalRasterPlacement parent = await CreatePlacementAsync(
			parentResource,
			transport,
			imageId: 77,
			placementId: 1
		);
		TerminalRasterResource childResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 88,
			imageNumber: 2
		);
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			childResource.CreateRelativePlacementAsync(
				parent,
				columnOffset: -2,
				rowOffset: 3,
				new TerminalRasterPlacementOptions {
					Columns = 4,
					Rows = 2,
					ZIndex = -1
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );

		Assert.False( creation.IsCompleted );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=88,p=2,C=1,P=77,Q=1,H=-2,V=3,c=4,r=2,z=-1\u001b\\"
			),
			transport.Writes[ expectedWriteCount - 1 ]
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=2;OK\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=88,p=3;OK\u001b\\" )
		);
		Assert.False( creation.IsCompleted );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=88,p=2;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterPlacement> result = await creation;
		TerminalRasterPlacement child = Assert.IsType<TerminalRasterPlacement>( result.Value );

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.Same( parent.State, child.State.Parent );
		Assert.Equal( 1, child.State.RelativeDepth );
		Assert.Equal( -2, child.State.ColumnOffset );
		Assert.Equal( 3, child.State.RowOffset );
	}

	[Fact]
	public async Task SuccessfulRelativeUpdateCommitsOffsetsOnlyAfterAcknowledgement() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		(
			TerminalRasterPlacement parent,
			TerminalRasterPlacement child
		) = await CreateRelativePairAsync( session, transport );
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlMutationResult> update = child.UpdateRelativeAsync(
			columnOffset: 5,
			rowOffset: -6,
			new TerminalRasterPlacementOptions {
				Columns = 3,
				ZIndex = 4
			}
		).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );

		Assert.False( update.IsCompleted );
		Assert.Same( parent.State, child.State.Parent );
		Assert.Equal( -2, child.State.ColumnOffset );
		Assert.Equal( 3, child.State.RowOffset );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=88,p=2,C=1,P=77,Q=1,H=5,V=-6,c=3,z=4\u001b\\"
			),
			transport.Writes[ expectedWriteCount - 1 ]
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=88,p=2;OK\u001b\\" )
		);
		TerminalControlMutationResult result = await update;

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.Same( parent.State, child.State.Parent );
		Assert.Equal( 5, child.State.ColumnOffset );
		Assert.Equal( -6, child.State.RowOffset );
	}

	[Fact]
	public async Task FailedRelativeUpdateRetainsLastAcknowledgedOffsets() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		(
			TerminalRasterPlacement parent,
			TerminalRasterPlacement child
		) = await CreateRelativePairAsync( session, transport );
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlMutationResult> update = child.UpdateRelativeAsync(
			columnOffset: 9,
			rowOffset: -10
		).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );

		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=88,p=2,C=1,P=77,Q=1,H=9,V=-10\u001b\\"
			),
			transport.Writes[ expectedWriteCount - 1 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=88,p=2;EINVAL:synthetic rejected update\u001b\\"
			)
		);
		TerminalControlMutationResult result = await update;

		Assert.Equal( TerminalControlStatus.Failed, result.Status );
		Assert.Same( parent.State, child.State.Parent );
		Assert.Equal( -2, child.State.ColumnOffset );
		Assert.Equal( 3, child.State.RowOffset );
	}

	[Fact]
	public async Task CommonUpdateRetainsImmutableParentAndAcknowledgedOffsets() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		(
			TerminalRasterPlacement parent,
			TerminalRasterPlacement child
		) = await CreateRelativePairAsync( session, transport );
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlMutationResult> update = child.UpdateAsync(
			new TerminalRasterPlacementOptions {
				Rows = 5,
				ZIndex = -4
			}
		).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );

		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=88,p=2,C=1,P=77,Q=1,H=-2,V=3,r=5,z=-4\u001b\\"
			),
			transport.Writes[ expectedWriteCount - 1 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=88,p=2;OK\u001b\\" )
		);
		TerminalControlMutationResult result = await update;

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.Same( parent.State, child.State.Parent );
		Assert.Equal( -2, child.State.ColumnOffset );
		Assert.Equal( 3, child.State.RowOffset );
	}

	private static async Task<(
		TerminalRasterPlacement Parent,
		TerminalRasterPlacement Child
	)> CreateRelativePairAsync(
		TerminalSession session,
		ScriptedTransport transport
	) {
		TerminalRasterResource parentResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		TerminalRasterPlacement parent = await CreatePlacementAsync(
			parentResource,
			transport,
			imageId: 77,
			placementId: 1
		);
		TerminalRasterResource childResource = await CreateResourceAsync(
			session,
			transport,
			imageId: 88,
			imageNumber: 2
		);
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			childResource.CreateRelativePlacementAsync(
				parent,
				columnOffset: -2,
				rowOffset: 3
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=88,p=2;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterPlacement> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return (
			parent,
			Assert.IsType<TerminalRasterPlacement>( result.Value )
		);
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId,
		uint imageNumber
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( transport );
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync( CreateSmallImage() ).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},I={imageNumber};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async Task<TerminalRasterPlacement> CreatePlacementAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		uint imageId,
		uint placementId
	) {
		ArgumentNullException.ThrowIfNull( resource );
		ArgumentNullException.ThrowIfNull( transport );
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreatePlacementAsync().AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},p={placementId};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterPlacement> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterPlacement>( result.Value );
	}

	private static TerminalRasterImage CreateSmallImage() {
		return TerminalRasterImage.CreateRgb24(
			1,
			1,
			[ 1, 2, 3 ]
		);
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
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
						static item => item.ToArray()
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
			using CancellationTokenSource timeout = new();
			timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
			while ( this.Writes.Count < expected ) {
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
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
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
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
