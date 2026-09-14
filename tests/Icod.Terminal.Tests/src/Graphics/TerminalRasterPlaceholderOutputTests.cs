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
/// Freezes the T154 current-cursor raster-placeholder output contract.
/// </summary>
public sealed class TerminalRasterPlaceholderOutputTests {
	[Fact]
	public async Task SingleCellWritesExactCurrentCursorBytesWithoutQueryRoundTrip() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			session,
			transport,
			imageId: 0x0200002Au,
			columns: 2,
			rows: 2
		);
		TerminalRasterPlaceholderCell cell = placeholder.GetCell( 1, 0 );
		int baselineWrites = transport.Writes.Count;

		await session.WriteRasterPlaceholderCellAsync( cell );

		Assert.Equal( baselineWrites + 1, transport.Writes.Count );
		Assert.Equal(
			KittyGraphicsPlaceholderCellEncoder.Encode(
				placeholder.State,
				row: 1,
				column: 0
			).ToArray(),
			transport.Writes[ baselineWrites ]
		);
		Assert.False( transport.WriteCancellationCanBeCanceled[ baselineWrites ] );
	}

	[Fact]
	public async Task BulkOutputPreservesCallerOrderAndWritesCellsIndependently() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			session,
			transport,
			imageId: 0x0200002Au,
			columns: 2,
			rows: 2
		);
		TerminalRasterPlaceholderCell[] cells = [
			placeholder.GetCell( 1, 1 ),
			placeholder.GetCell( 0, 1 ),
			placeholder.GetCell( 0, 0 ),
		];
		int baselineWrites = transport.Writes.Count;

		await session.WriteRasterPlaceholderCellsAsync( cells );

		Assert.Equal( baselineWrites + cells.Length, transport.Writes.Count );
		for ( int index = 0; index < cells.Length; ++index ) {
			Assert.Equal(
				KittyGraphicsPlaceholderCellEncoder.Encode(
					placeholder.State,
					cells[ index ].Row,
					cells[ index ].Column
				).ToArray(),
				transport.Writes[ baselineWrites + index ]
			);
			Assert.False(
				transport.WriteCancellationCanBeCanceled[ baselineWrites + index ]
			);
		}
	}

	[Fact]
	public async Task OutputWaitsForSessionGateAndHonorsCancellationBeforeCommitment() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			session,
			transport,
			imageId: 77u,
			columns: 1,
			rows: 1
		);
		TerminalRasterPlaceholderCell cell = placeholder.GetCell( 0, 0 );
		int baselineWrites = transport.Writes.Count;
		IDisposable outputLease = await session.AcquireSessionOutputAsync(
			CancellationToken.None
		);
		using CancellationTokenSource cancellation = new();
		try {
			Task output = session.WriteRasterPlaceholderCellAsync(
				cell,
				cancellation.Token
			).AsTask();
			await Task.Yield();
			Assert.False( output.IsCompleted );
			cancellation.Cancel();
			outputLease.Dispose();
			await Assert.ThrowsAnyAsync<OperationCanceledException>(
				() => output
			);
		} finally {
			outputLease.Dispose();
		}

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task CrossSessionCellFailsBeforeOutput() {
		ScriptedTransport firstTransport = new();
		ScriptedTransport secondTransport = new();
		await using TerminalSession first = await OpenSessionAsync( firstTransport );
		await using TerminalSession second = await OpenSessionAsync( secondTransport );
		TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			first,
			firstTransport,
			imageId: 77u,
			columns: 1,
			rows: 1
		);
		TerminalRasterPlaceholderCell cell = placeholder.GetCell( 0, 0 );
		int baselineWrites = secondTransport.Writes.Count;

		await Assert.ThrowsAsync<ArgumentException>(
			() => second.WriteRasterPlaceholderCellAsync( cell ).AsTask()
		);

		Assert.Equal( baselineWrites, secondTransport.Writes.Count );
	}

	[Fact]
	public async Task DisposedPlaceholderCellFailsBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			session,
			transport,
			imageId: 77u,
			columns: 1,
			rows: 1
		);
		TerminalRasterPlaceholderCell cell = placeholder.GetCell( 0, 0 );
		await placeholder.DisposeAsync();
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<ObjectDisposedException>(
			() => session.WriteRasterPlaceholderCellAsync( cell ).AsTask()
		);

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task StaleAndReleasedPlaceholderCellsFailBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterPlaceholder stale = await CreatePlaceholderAsync(
			session,
			transport,
			imageId: 77u,
			columns: 1,
			rows: 1,
			expectedPlacementId: 1u
		);
		TerminalRasterResource resource = stale.State.Resource is null
			? throw new InvalidOperationException()
			: new TerminalRasterResource(
				session,
				stale.State.Resource
			);
		TerminalRasterPlaceholder released = await CreatePlaceholderOnResourceAsync(
			resource,
			transport,
			imageId: 77u,
			columns: 1,
			rows: 1,
			expectedPlacementId: 2u
		);
		TerminalRasterPlaceholderCell staleCell = stale.GetCell( 0, 0 );
		TerminalRasterPlaceholderCell releasedCell = released.GetCell( 0, 0 );
		Assert.True(
			stale.State.TryMarkStale(
				TerminalRasterOwnershipLossReason.SessionStateLost
			)
		);
		Assert.True(
			released.State.TryMarkReleased(
				TerminalRasterOwnershipLossReason.ResourceReleased
			)
		);
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.WriteRasterPlaceholderCellAsync( staleCell ).AsTask()
		);
		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.WriteRasterPlaceholderCellAsync( releasedCell ).AsTask()
		);

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	[Fact]
	public async Task UnregisteredCurrentCellFailsBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalPersistentRasterResourceState resource = new(
			imageNumber: 99u,
			generation: 99L
		);
		resource.BindImageId( 77u );
		TerminalPersistentRasterPlaceholderState state = new(
			resource,
			placementId: 1u,
			generation: 99L,
			columns: 1,
			rows: 1
		);
		TerminalRasterPlaceholder placeholder = new(
			session,
			state
		);
		TerminalRasterPlaceholderCell cell = placeholder.GetCell( 0, 0 );
		int baselineWrites = transport.Writes.Count;

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.WriteRasterPlaceholderCellAsync( cell ).AsTask()
		);

		Assert.Equal( baselineWrites, transport.Writes.Count );
	}

	private static async Task<TerminalRasterPlaceholder> CreatePlaceholderAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId,
		int columns,
		int rows,
		uint expectedPlacementId = 1u
	) {
		TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId
		);
		return await CreatePlaceholderOnResourceAsync(
			resource,
			transport,
			imageId,
			columns,
			rows,
			expectedPlacementId
		);
	}

	private static async Task<TerminalRasterPlaceholder> CreatePlaceholderOnResourceAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		uint imageId,
		int columns,
		int rows,
		uint expectedPlacementId
	) {
		int expectedWriteCount = transport.Writes.Count + 1;
		Task<TerminalControlResult<TerminalRasterPlaceholder>> creation =
			resource.CreatePlaceholderAsync(
				new TerminalRasterPlaceholderOptions {
					Columns = columns,
					Rows = rows
				}
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},p={expectedPlacementId};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterPlaceholder> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterPlaceholder>( result.Value );
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId
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
		private readonly List<bool> writeCancellationCanBeCanceled = [];

		internal IReadOnlyList<byte[]> Writes {
			get {
				lock ( this.synchronization ) {
					return this.writes.Select(
						static item => item.ToArray()
					).ToArray();
				}
		}

		internal IReadOnlyList<bool> WriteCancellationCanBeCanceled {
			get {
				lock ( this.synchronization ) {
					return this.writeCancellationCanBeCanceled.ToArray();
				}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] value = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
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
				this.writeCancellationCanBeCanceled.Add(
					cancellationToken.CanBeCanceled
				);
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
