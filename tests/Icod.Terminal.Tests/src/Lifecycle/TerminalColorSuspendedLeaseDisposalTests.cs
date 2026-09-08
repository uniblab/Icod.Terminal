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
namespace Icod.Terminal.Tests.Lifecycle;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies that scoped color ownership released while suspended does not return
/// during the next lifecycle epoch.
/// </summary>
public sealed class TerminalColorSuspendedLeaseDisposalTests {
	[Fact]
	public async Task LeaseDisposedWhileSuspendedIsNotObservedOrReappliedAfterResume() {
		RecordingTransport transport = new();
		TestLifecycleSource lifecycle = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			lifecycle
		);
		TerminalPaletteColorLease released = await AcquireAsync(
			session,
			transport,
			4,
			new TerminalColor( 0xaaaa, 0xbbbb, 0xcccc ),
			new TerminalColor( 0x1111, 0x2222, 0x3333 )
		);
		TerminalPaletteColorLease retained = await AcquireAsync(
			session,
			transport,
			5,
			new TerminalColor( 0x4444, 0x5555, 0x6666 ),
			new TerminalColor( 0x7777, 0x8888, 0x9999 )
		);
		using CancellationTokenSource timeout = new(
			TimeSpan.FromSeconds( 5 )
		);

		int beforeSuspend = transport.WriteCount;
		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		await WaitForWriteCountAsync( transport, beforeSuspend + 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;4;rgb:1111/2222/3333\u001b\\" ),
			transport.GetWrite( beforeSuspend )
		);
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:7777/8888/9999\u001b\\" ),
			transport.GetWrite( beforeSuspend + 1 )
		);

		int beforeRelease = transport.WriteCount;
		await released.DisposeAsync();
		Assert.Equal( beforeRelease, transport.WriteCount );

		lifecycle.Publish( TerminalLifecycleSignalKind.Resume );
		await WaitForWriteCountAsync( transport, beforeRelease + 1 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;?\u001b\\" ),
			transport.GetWrite( beforeRelease )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:dead/beef/1234\u001b\\" )
		);

		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		await WaitForWriteCountAsync( transport, beforeRelease + 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:4444/5555/6666\u001b\\" ),
			transport.GetWrite( beforeRelease + 1 )
		);

		IReadOnlyList<string> resumeWrites = transport.GetWrites()
			.Skip( beforeRelease )
			.Select( static bytes => Encoding.ASCII.GetString( bytes ) )
			.ToArray();
		Assert.DoesNotContain( "\u001b]4;4;?\u001b\\", resumeWrites );
		Assert.DoesNotContain( "\u001b]4;4;rgb:aaaa/bbbb/cccc\u001b\\", resumeWrites );

		await retained.DisposeAsync();
		await WaitForWriteCountAsync( transport, beforeRelease + 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b]4;5;rgb:dead/beef/1234\u001b\\" ),
			transport.GetWrite( beforeRelease + 2 )
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
		RecordingTransport transport,
		TestLifecycleSource lifecycle
	) {
		ArgumentNullException.ThrowIfNull( transport );
		ArgumentNullException.ThrowIfNull( lifecycle );
		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				LifecycleSource = lifecycle
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

		internal IReadOnlyList<byte[]> GetWrites() {
			lock ( this.writes ) {
				return this.writes.Select( static bytes => bytes.ToArray() ).ToArray();
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

	private sealed class TestLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			if ( !this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) ) ) {
				throw new InvalidOperationException( "The lifecycle test queue rejected a signal." );
			}
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
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
						| TerminalControlCapabilities.LiveSize
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
				new TerminalSize( 80, 24 )
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
