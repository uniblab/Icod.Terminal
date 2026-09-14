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
/// Provides bounded T156 concurrency qualification for raster-placeholder ownership and output.
/// </summary>
public sealed class TerminalRasterPlaceholderConcurrencyHardeningTests {
	private const int ReaderCount = 8;
	private const int ReaderIterationCount = 256;
	private const int OutputCount = 24;

	[Fact]
	public async Task ConcurrentOwnershipAndGetCellReadsRemainStableAtMaximumDimensions() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		await using TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			resource,
			transport,
			imageId: 77u,
			columns: 256,
			rows: 256
		);

		Task[] readers = Enumerable.Range(
			0,
			ReaderCount
		).Select(
			worker => Task.Run(
				() => {
					for ( int iteration = 0; iteration < ReaderIterationCount; ++iteration ) {
						TerminalRasterOwnershipState ownership = placeholder.OwnershipState;
						Assert.Equal(
							TerminalRasterOwnershipStatus.Current,
							ownership.Status
						);
						Assert.Equal(
							TerminalRasterOwnershipLossReason.None,
							ownership.LossReason
						);

						int row = ( worker + iteration ) & 0xFF;
						int column = ( ( worker * 31 ) + iteration ) & 0xFF;
						TerminalRasterPlaceholderCell cell = placeholder.GetCell(
							row,
							column
						);
						Assert.Equal( row, cell.Row );
						Assert.Equal( column, cell.Column );
					}
				}
			)
		).ToArray();

		await Task.WhenAll( readers );

		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Current,
				TerminalRasterOwnershipLossReason.None
			),
			placeholder.OwnershipState
		);
	}

	[Fact]
	public async Task ConcurrentPlaceholderOutputIsSerializedAndComplete() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		await using TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			resource,
			transport,
			imageId: 77u,
			columns: 2,
			rows: 2
		);
		TerminalRasterPlaceholderCell cell = placeholder.GetCell( 1, 1 );
		byte[] expected = KittyGraphicsPlaceholderCellEncoder.Encode(
			placeholder.State,
			row: 1,
			column: 1
		).ToArray();
		int baselineWrites = transport.Writes.Count;

		Task[] outputs = Enumerable.Range(
			0,
			OutputCount
		).Select(
			_ => session.WriteRasterPlaceholderCellAsync( cell ).AsTask()
		).ToArray();
		await Task.WhenAll( outputs );

		Assert.Equal( baselineWrites + OutputCount, transport.Writes.Count );
		for ( int index = baselineWrites; index < baselineWrites + OutputCount; ++index ) {
			Assert.Equal( expected, transport.Writes[ index ] );
		}
	}

	[Fact]
	public async Task ConcurrentOwnershipReadsRacingDisposalNeverResurrectOwnership() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		TerminalRasterPlaceholder placeholder = await CreatePlaceholderAsync(
			resource,
			transport,
			imageId: 77u,
			columns: 4,
			rows: 4
		);
		TaskCompletionSource start = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

		Task[] readers = Enumerable.Range(
			0,
			ReaderCount
		).Select(
			_ => Task.Run(
				async () => {
					await start.Task;
					for ( int iteration = 0; iteration < ReaderIterationCount; ++iteration ) {
						TerminalRasterOwnershipState ownership = placeholder.OwnershipState;
						Assert.True(
							ownership == new TerminalRasterOwnershipState(
								TerminalRasterOwnershipStatus.Current,
								TerminalRasterOwnershipLossReason.None
							) || ownership == new TerminalRasterOwnershipState(
								TerminalRasterOwnershipStatus.Disposed,
								TerminalRasterOwnershipLossReason.ExplicitDisposal
							)
						);
					}
				}
			)
		).ToArray();
		Task disposal = Task.Run(
			async () => {
				await start.Task;
				await placeholder.DisposeAsync();
			}
		);

		start.SetResult();
		await Task.WhenAll( readers.Append( disposal ) );

		Assert.Equal(
			new TerminalRasterOwnershipState(
				TerminalRasterOwnershipStatus.Disposed,
				TerminalRasterOwnershipLossReason.ExplicitDisposal
			),
			placeholder.OwnershipState
		);
	}

	private static async Task<TerminalRasterPlaceholder> CreatePlaceholderAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		uint imageId,
		int columns,
		int rows
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
				$"\u001b_Gi={imageId},p=1;OK\u001b\\"
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
			while ( true ) {
				lock ( this.synchronization ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}
	}
}
