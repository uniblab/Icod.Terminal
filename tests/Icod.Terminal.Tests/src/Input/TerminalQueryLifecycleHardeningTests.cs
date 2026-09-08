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
namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies 0.18 query-generation and lifecycle-observation hardening.
/// </summary>
public sealed class TerminalQueryLifecycleHardeningTests {
	private static readonly byte[] ResponseOne = Encoding.Latin1.GetBytes(
		"\u001b[1;2R"
	);
	private static readonly byte[] ResponseTwo = Encoding.Latin1.GetBytes(
		"\u001b[3;4R"
	);
	private static readonly byte[] ResponseThree = Encoding.Latin1.GetBytes(
		"\u001b[5;6R"
	);

	[Fact]
	public async Task LifecycleObservationWaitsForPreSuspendLateOwnership() {
		HardeningTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalResponseFrame> stale = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 ),
			TimeSpan.FromSeconds( 10 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		session.SuspendQueryTransactions();
		await Assert.ThrowsAsync<InvalidOperationException>( () => stale );
		session.BeginLifecycleObservationQueryWindow();

		try {
			Task<TerminalResponseFrame> observation =
				session.ExecuteLifecycleObservationQueryAsync(
					Encoding.ASCII.GetBytes( "Q2" ),
					new ExactResponseMatcher( ResponseTwo ),
					TimeSpan.FromSeconds( 30 )
				).AsTask();

			await YieldSeveralTimesAsync();
			Assert.Equal( 1, transport.WriteCount );

			transport.Publish( ResponseOne );
			await transport.WaitForWriteCountAsync( 2 );
			Assert.Equal( "Q2", transport.GetWriteText( 1 ) );

			transport.Publish( ResponseTwo );
			Assert.Equal(
				ResponseTwo,
				( await observation ).Bytes.ToArray()
			);
		} finally {
			session.EndLifecycleObservationQueryWindow();
			session.ResumeQueryTransactions();
		}
	}

	[Fact]
	public async Task QueuedOldGenerationDoesNotEmitBeforeLifecycleObservation() {
		HardeningTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalResponseFrame> active = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q1" ),
			new ExactResponseMatcher( ResponseOne ),
			TimeSpan.FromSeconds( 30 ),
			TimeSpan.FromSeconds( 10 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		Task<TerminalResponseFrame> queued = session.ExecuteQueryAsync(
			Encoding.ASCII.GetBytes( "Q2" ),
			new ExactResponseMatcher( ResponseTwo ),
			TimeSpan.FromSeconds( 30 )
		).AsTask();

		session.SuspendQueryTransactions();
		await Assert.ThrowsAsync<InvalidOperationException>( () => active );
		await Assert.ThrowsAsync<InvalidOperationException>( () => queued );
		session.BeginLifecycleObservationQueryWindow();

		try {
			Task<TerminalResponseFrame> observation =
				session.ExecuteLifecycleObservationQueryAsync(
					Encoding.ASCII.GetBytes( "Q3" ),
					new ExactResponseMatcher( ResponseThree ),
					TimeSpan.FromSeconds( 30 )
				).AsTask();

			await YieldSeveralTimesAsync();
			Assert.Equal( 1, transport.WriteCount );

			transport.Publish( ResponseOne );
			await transport.WaitForWriteCountAsync( 2 );

			Assert.Equal( "Q1", transport.GetWriteText( 0 ) );
			Assert.Equal( "Q3", transport.GetWriteText( 1 ) );
			Assert.DoesNotContain(
				"Q2",
				transport.GetWritesAsText()
			);

			transport.Publish( ResponseThree );
			Assert.Equal(
				ResponseThree,
				( await observation ).Bytes.ToArray()
			);
		} finally {
			session.EndLifecycleObservationQueryWindow();
			session.ResumeQueryTransactions();
		}
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		HardeningTransport transport
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
				ObserveLifecycleEvents = false,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static async Task YieldSeveralTimesAsync() {
		for ( int count = 0; count < 32; count++ ) {
			await Task.Yield();
		}
	}

	private sealed class ExactResponseMatcher : ITerminalResponseMatcher {
		private readonly byte[] expected;

		internal ExactResponseMatcher(
			byte[] expected
		) {
			ArgumentNullException.ThrowIfNull( expected );
			this.expected = expected.ToArray();
		}

		public TerminalResponseFrameKind FrameKind {
			get;
		} = TerminalResponseFrameKind.Csi;

		public bool IsMatch(
			TerminalResponseFrame frame
		) {
			ArgumentNullException.ThrowIfNull( frame );
			return TerminalResponseFrameKind.Csi == frame.Kind
				&& frame.Bytes.Span.SequenceEqual( this.expected )
			;
		}
	}

	private sealed class HardeningTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly List<byte[]> writes = [];
		private readonly SemaphoreSlim writeSignal = new( 0 );

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
			}
		}

		internal string GetWriteText(
			int index
		) {
			lock ( this.sync ) {
				return Encoding.ASCII.GetString( this.writes[ index ] );
			}
		}

		internal IReadOnlyList<string> GetWritesAsText() {
			lock ( this.sync ) {
				return this.writes
					.Select( Encoding.ASCII.GetString )
					.ToArray();
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
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new(
				TimeSpan.FromSeconds( 5 )
			);
			while ( expected > this.WriteCount ) {
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
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
			return TerminalControlResult<TerminalModeSnapshot>.Available(
				this.baseline
			);
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
