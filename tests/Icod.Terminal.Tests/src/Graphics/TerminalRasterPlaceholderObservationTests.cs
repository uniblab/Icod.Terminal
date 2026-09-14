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
using Xunit;

/// <summary>
/// Freezes the T152 acknowledged virtual-placement creation and lifecycle contract.
/// </summary>
public sealed class TerminalRasterPlaceholderObservationTests {
	[Fact]
	public async Task PlaceholderCreationUsesVirtualPlacementAndWaitsForCorrelatedAcknowledgement() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlResult<TerminalRasterPlaceholder>> creation =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 3,
					Rows = 2
				}
			).AsTask();

		Assert.False( creation.IsCompleted );
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=p,i=77,p=1,C=1,U=1,c=3,r=2\u001b\\"
			),
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
		TerminalControlResult<TerminalRasterPlaceholder> result = await creation;

		Assert.Equal( TerminalControlStatus.Available, result.Status );
		TerminalRasterPlaceholder placeholder = Assert.IsType<TerminalRasterPlaceholder>( result.Value );
		Assert.Equal( 3, placeholder.Columns );
		Assert.Equal( 2, placeholder.Rows );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			placeholder.OwnershipState
		);
	}

	[Fact]
	public async Task PlaceholderEnoentInvalidatesOwningResourceAndPublishesNoHandle() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
		int expectedWriteCount = transport.Writes.Count + 1;

		Task<TerminalControlResult<TerminalRasterPlaceholder>> creation =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 2,
					Rows = 2
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				"\u001b_Gi=77,p=1;ENOENT:synthetic missing image\u001b\\"
			)
		);

		TerminalControlResult<TerminalRasterPlaceholder> result = await creation;
		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Null( result.Value );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Stale,
				TerminalRasterOwnershipLossReason.ResourceMissing
			),
			resource.OwnershipState
		);

		int baselineWrites = transport.Writes.Count;
		TerminalControlResult<TerminalRasterPlaceholder> retry =
			await resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			);
		Assert.Equal( TerminalControlStatus.Unavailable, retry.Status );
		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task MalformedCorrelatedPlaceholderAcknowledgementRollsBackReservation() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77,
			imageNumber: 1
		);
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
				"\u001b_Gi=77,p=1,p=1;OK\u001b\\"
			)
		);

		await Assert.ThrowsAsync<FormatException>( () => creation );
		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			resource.OwnershipState
		);

		int retryWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlaceholder>> retry =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = 1,
					Rows = 1
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( retryWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77,p=2;OK\u001b\\" )
		);
		Assert.Equal( TerminalControlStatus.Available, ( await retry ).Status );
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
				$"\u001b_Gi={imageId},I={imageNumber};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
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
