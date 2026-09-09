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
namespace Icod.Terminal.Tests.Session;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies C163 pixel-geometry queries on the authoritative session query path.
/// </summary>
public sealed class TerminalGeometrySessionTests {
	[Fact]
	public async Task TerminalAndCellPixelQueriesUseOneAuthoritativeInputPath() {
		GeometryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPixelSize> terminalQuery = session.QueryTerminalPixelSizeAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[14t" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[4;800;1200t" )
		);
		TerminalPixelSize terminalSize = await terminalQuery;

		Task<TerminalPixelSize> cellQuery = session.QueryCellPixelSizeAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[16t" ),
			transport.GetWrite( 1 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b[6;20;10t" )
		);
		TerminalPixelSize cellSize = await cellQuery;

		Assert.Equal( 1200, terminalSize.Width );
		Assert.Equal( 800, terminalSize.Height );
		Assert.Equal( 10, cellSize.Width );
		Assert.Equal( 20, cellSize.Height );
		Assert.Equal( 1, transport.MaximumConcurrentReads );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		GeometryTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );

		return TerminalSession.OpenAsync(
			new GeometryTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		GeometryTransport transport,
		int expected
	) {
		ArgumentNullException.ThrowIfNull( transport );
		if ( 0 > expected ) {
			throw new ArgumentOutOfRangeException( nameof( expected ) );
		}

		using CancellationTokenSource timeout = new();
		timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
		await transport.WaitForWriteCountAsync(
			expected,
			timeout.Token
		);
	}

	private sealed class GeometryTerminalControlProvider : ITerminalControlProvider {
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
			return TerminalControlResult<TerminalSize>.Available(
				new TerminalSize( 120, 40 )
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

	private sealed class GeometryTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly List<byte[]> writes = [];
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private int activeReads;
		private int maximumConcurrentReads;

		internal int MaximumConcurrentReads {
			get {
				return Volatile.Read( ref this.maximumConcurrentReads );
			}
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.sync ) {
				return this.writes[ index ].ToArray();
			}
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

		internal async ValueTask WaitForWriteCountAsync(
			int expected,
			CancellationToken cancellationToken
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}
			cancellationToken.ThrowIfCancellationRequested();

			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}

				await this.writeSignal.WaitAsync(
					cancellationToken
				).ConfigureAwait( false );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			if ( buffer.IsEmpty ) {
				return 0;
			}

			int active = Interlocked.Increment( ref this.activeReads );
			RecordMaximum(
				ref this.maximumConcurrentReads,
				active
			);
			try {
				byte[] bytes = await this.input.Reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				if ( bytes.Length > buffer.Length ) {
					throw new InvalidOperationException(
						"The scripted input chunk exceeds the decoder read buffer."
					);
				}

				bytes.AsSpan().CopyTo( buffer.Span );
				return bytes.Length;
			} finally {
				Interlocked.Decrement( ref this.activeReads );
			}
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.sync ) {
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

		private static void RecordMaximum(
			ref int target,
			int candidate
		) {
			while ( true ) {
				int observed = Volatile.Read( ref target );
				if ( candidate <= observed ) {
					return;
				}
				if ( observed == Interlocked.CompareExchange(
					ref target,
					candidate,
					observed
				) ) {
					return;
				}
			}
		}
	}
}
