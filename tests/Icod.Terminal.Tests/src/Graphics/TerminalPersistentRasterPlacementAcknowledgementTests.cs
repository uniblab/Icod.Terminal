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
/// Defines C118 acknowledgement ownership and terminal-side disappearance handling for
/// persistent raster placements.
/// </summary>
public sealed class TerminalPersistentRasterPlacementAcknowledgementTests {
	[Fact]
	public async Task PlacementCreationWaitsForMatchingImageAndPlacementAcknowledgement() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreatePlacementAsync().AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );

		Assert.False( creation.IsCompleted );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=p,i=77,p=1,C=1\u001b\\" ),
			transport.Writes[ expectedWriteCount - 1 ]
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=88,p=1;OK\u001b\\" )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=2;OK\u001b\\" )
		);
		Assert.False( creation.IsCompleted );

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=1;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterPlacement> result = await creation;

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		Assert.NotNull( result.Value );
	}

	[Fact]
	public async Task PlacementCreationEnoentInvalidatesOwningResource() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreatePlacementAsync().AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=77,p=1;ENOENT:synthetic missing image\u001b\\"
			)
		);

		TerminalControlResult<TerminalRasterPlacement> result = await creation;
		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Null( result.Value );

		int baselineWrites = transport.Writes.Count;
		TerminalControlResult<TerminalRasterPlacement> retry =
			await resource.CreatePlacementAsync();
		Assert.Equal( TerminalControlStatus.Unavailable, retry.Status );
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task PlacementUpdateEnoentInvalidatesResourceAndPlacement() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		TerminalRasterPlacement placement = await CreatePlacementAsync(
			resource,
			transport,
			imageId: 77,
			placementId: 1
		);
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlMutationResult> update = placement.UpdateAsync(
			new TerminalRasterPlacementOptions {
				Columns = 3,
				Rows = 2
			}
		).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );

		Assert.False( update.IsCompleted );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=77,p=1,C=1,c=3,r=2\u001b\\"
			),
			transport.Writes[ expectedWriteCount - 1 ]
		);
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=77,p=1;ENOENT:synthetic missing image\u001b\\"
			)
		);

		TerminalControlMutationResult result = await update;
		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );

		int baselineWrites = transport.Writes.Count;
		Assert.Equal(
			TerminalControlStatus.Unavailable,
			( await placement.UpdateAsync() ).Status
		);
		Assert.Equal(
			TerminalControlStatus.Unavailable,
			( await resource.CreatePlacementAsync() ).Status
		);
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task MalformedCorrelatedPlacementAcknowledgementThrowsFormatException() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlResult<TerminalRasterPlacement>> creation =
			resource.CreatePlacementAsync().AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=77,p=1,p=1;OK\u001b\\"
			)
		);

		await Assert.ThrowsAsync<FormatException>( () => creation );
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId,
		uint imageNumber
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}
		if ( 0u == imageNumber ) {
			throw new ArgumentOutOfRangeException( nameof( imageNumber ) );
		}

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
		if ( 0u == imageId ) {
			throw new ArgumentOutOfRangeException( nameof( imageId ) );
		}
		if ( 0u == placementId ) {
			throw new ArgumentOutOfRangeException( nameof( placementId ) );
		}

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
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new(
				TimeSpan.FromSeconds( 5 )
			);
			while ( true ) {
				lock ( this.synchronization ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}
	}
}
