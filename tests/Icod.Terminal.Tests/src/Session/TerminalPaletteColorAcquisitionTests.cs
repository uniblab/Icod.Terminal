namespace Icod.Terminal.Tests.Session;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies first-owner failure safety and independent indexed-palette ownership.
/// </summary>
public sealed class TerminalPaletteColorAcquisitionTests {
	[Fact]
	public async Task MalformedFirstOwnerBaselineFailsWithoutPaletteMutation() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalPaletteColorLease> acquisition = session.AcquirePaletteColorAsync(
			12,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;12;?\u001b\\" ),
			transport.GetWrite( 0 )
		);

		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;12;not-a-color\u001b\\" )
		);

		await Assert.ThrowsAsync<FormatException>( () => acquisition );
		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public async Task DifferentIndicesMaintainIndependentBaselinesAndOwners() {
		RecordingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalPaletteColorLease first = await AcquireAsync(
			session,
			transport,
			1,
			new TerminalColor( 0xaaaa, 0x1111, 0x2222 ),
			new TerminalColor( 0x0101, 0x0202, 0x0303 )
		);
		TerminalPaletteColorLease second = await AcquireAsync(
			session,
			transport,
			2,
			new TerminalColor( 0xbbbb, 0x3333, 0x4444 ),
			new TerminalColor( 0x0404, 0x0505, 0x0606 )
		);
		Assert.Equal( 4, transport.WriteCount );

		await first.DisposeAsync();
		await WaitForWriteCountAsync( transport, 5 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;1;rgb:0101/0202/0303\u001b\\" ),
			transport.GetWrite( 4 )
		);

		await second.DisposeAsync();
		await WaitForWriteCountAsync( transport, 6 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;2;rgb:0404/0505/0606\u001b\\" ),
			transport.GetWrite( 5 )
		);
	}

	private static async ValueTask<TerminalPaletteColorLease> AcquireAsync(
		TerminalSession session,
		RecordingTransport transport,
		byte index,
		TerminalColor requested,
		TerminalColor baseline
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( transport );

		int initialWrites = transport.WriteCount;
		Task<TerminalPaletteColorLease> acquisition = session.AcquirePaletteColorAsync(
			index,
			requested,
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await WaitForWriteCountAsync( transport, initialWrites + 1 );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b]4;{index};rgb:{baseline.Red:x4}/{baseline.Green:x4}/{baseline.Blue:x4}\u001b\\"
			)
		);
		TerminalPaletteColorLease lease = await acquisition;
		await WaitForWriteCountAsync( transport, initialWrites + 2 );
		return lease;
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RecordingTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private static async Task WaitForWriteCountAsync(
		RecordingTransport transport,
		int count
	) {
		ArgumentNullException.ThrowIfNull( transport );
		for ( int attempt = 0; attempt < 500; ++attempt ) {
			if ( count <= transport.WriteCount ) {
				return;
			}
			await Task.Delay( 10 );
		}
		throw new TimeoutException( "The expected terminal output was not observed." );
	}

	private sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly List<byte[]> writes = [];
		private byte[]? pending;
		private int pendingOffset;

		internal int WriteCount {
			get {
				lock ( this.writes ) {
					return this.writes.Count;
				}
			}
		}

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.writes ) {
				return this.writes[ index ].ToArray();
			}
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException( "The test input channel is closed." );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.pending is null ) {
				this.pending = await this.input.Reader.ReadAsync(
					cancellationToken
				).ConfigureAwait( false );
				this.pendingOffset = 0;
			}

			int count = Math.Min(
				buffer.Length,
				this.pending.Length - this.pendingOffset
			);
			this.pending.AsSpan( this.pendingOffset, count ).CopyTo( buffer.Span );
			this.pendingOffset += count;
			if ( this.pendingOffset == this.pending.Length ) {
				this.pending = null;
				this.pendingOffset = 0;
			}
			return count;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.writes ) {
				this.writes.Add( buffer.ToArray() );
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalControlProvider : ITerminalControlProvider {
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
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not used by palette acquisition tests."
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
