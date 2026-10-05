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
/// Freezes the independently owned Terminal 1.27 appearance-reporting contract.
/// </summary>
public sealed class TerminalAppearanceReportingTests {
	private static readonly byte[] ModeQuery =
		Encoding.ASCII.GetBytes( "\u001b[?2031$p" );
	private static readonly byte[] ModeEnable =
		Encoding.ASCII.GetBytes( "\u001b[?2031h" );
	private static readonly byte[] ModeDisable =
		Encoding.ASCII.GetBytes( "\u001b[?2031l" );

	[Theory]
	[InlineData( 0, false, false )]
	[InlineData( 1, true, false )]
	[InlineData( 2, true, true )]
	[InlineData( 3, true, false )]
	[InlineData( 4, false, false )]
	public async Task ModeStateControlsAvailabilityAndExactRestoration(
		int state,
		bool available,
		bool toggles
	) {
		AppearanceReportingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalControlResult<TerminalAppearanceReportingLease>> acquisition =
			session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		Assert.Equal( ModeQuery, transport.GetWrite( 0 ) );
		transport.Publish( ModeResponse( state ) );

		TerminalControlResult<TerminalAppearanceReportingLease> result =
			await acquisition;
		Assert.Equal( available, result.IsAvailable );
		if ( !available ) {
			Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
			Assert.Equal( 1, transport.WriteCount );
			return;
		}

		TerminalAppearanceReportingLease lease = result.GetRequiredValue();
		Assert.Equal( toggles ? 2 : 1, transport.WriteCount );
		if ( toggles ) {
			Assert.Equal( ModeEnable, transport.GetWrite( 1 ) );
		}

		await lease.DisposeAsync();
		Assert.Equal( toggles ? 3 : 1, transport.WriteCount );
		if ( toggles ) {
			Assert.Equal( ModeDisable, transport.GetWrite( 2 ) );
		}
	}

	[Theory]
	[InlineData( 1 )]
	[InlineData( 3 )]
	public async Task AlreadyEnabledBaselinesAreNeverDisabled(
		int state
	) {
		AppearanceReportingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalControlResult<TerminalAppearanceReportingLease>> firstTask =
			session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish( ModeResponse( state ) );
		TerminalAppearanceReportingLease first =
			( await firstTask ).GetRequiredValue();
		TerminalAppearanceReportingLease second =
			( await session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			) ).GetRequiredValue();

		await second.DisposeAsync();
		await first.DisposeAsync();

		Assert.Equal( 1, transport.WriteCount );
		Assert.Equal( ModeQuery, transport.GetWrite( 0 ) );
	}

	[Fact]
	public async Task ConcurrentOwnersShareOneBaselineAndReleaseOutOfOrder() {
		AppearanceReportingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalControlResult<TerminalAppearanceReportingLease>> firstTask =
			session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		Task<TerminalControlResult<TerminalAppearanceReportingLease>> secondTask =
			session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await YieldSeveralTimesAsync();
		Assert.Equal( 1, transport.WriteCount );

		transport.Publish( ModeResponse( 2 ) );
		TerminalAppearanceReportingLease first =
			( await firstTask ).GetRequiredValue();
		TerminalAppearanceReportingLease second =
			( await secondTask ).GetRequiredValue();

		Assert.True( second.OwnerId > first.OwnerId );
		Assert.Equal( 2, transport.WriteCount );
		await first.DisposeAsync();
		Assert.Equal( 2, transport.WriteCount );
		await second.DisposeAsync();
		Assert.Equal( 3, transport.WriteCount );
		Assert.Equal( ModeDisable, transport.GetWrite( 2 ) );
	}

	[Fact]
	public async Task RepeatedSuccessfulDisposalIsIdempotent() {
		AppearanceReportingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalAppearanceReportingLease lease = await AcquireAsync(
			session,
			transport,
			2
		);

		await lease.DisposeAsync();
		await lease.DisposeAsync();

		Assert.Equal( 3, transport.WriteCount );
	}

	[Fact]
	public async Task EnableFailureAttemptsOneKnownSafeCleanup() {
		AppearanceReportingTransport transport = new( 2 );
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalControlResult<TerminalAppearanceReportingLease>> acquisition =
			session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish( ModeResponse( 2 ) );

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => acquisition
		);

		Assert.Equal( "Synthetic output failure 2.", exception.Message );
		Assert.Equal( 3, transport.WriteCount );
		Assert.Equal( ModeEnable, transport.GetWrite( 1 ) );
		Assert.Equal( ModeDisable, transport.GetWrite( 2 ) );
	}

	[Fact]
	public async Task EnableAndCleanupFailuresAreBothReported() {
		AppearanceReportingTransport transport = new( 2, 3 );
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalControlResult<TerminalAppearanceReportingLease>> acquisition =
			session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish( ModeResponse( 2 ) );

		AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
			() => acquisition
		);

		Assert.Equal( 2, exception.InnerExceptions.Count );
		Assert.Equal( 3, transport.WriteCount );
		Assert.Equal( ModeDisable, transport.GetWrite( 2 ) );
	}

	[Fact]
	public async Task RestorationFailureRetainsOwnershipForRetry() {
		AppearanceReportingTransport transport = new( 3 );
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalAppearanceReportingLease lease = await AcquireAsync(
			session,
			transport,
			2
		);

		await Assert.ThrowsAsync<IOException>( () => lease.DisposeAsync().AsTask() );
		Assert.Equal( 3, transport.WriteCount );
		await lease.DisposeAsync();
		Assert.Equal( 4, transport.WriteCount );
		Assert.Equal( ModeDisable, transport.GetWrite( 2 ) );
		Assert.Equal( ModeDisable, transport.GetWrite( 3 ) );
	}

	[Theory]
	[InlineData( "\u001b[?2031;5$y" )]
	[InlineData( "\u001b[?2031;2:0$y" )]
	public async Task MalformedModeResponseRemainsFormatException(
		string response
	) {
		AppearanceReportingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalControlResult<TerminalAppearanceReportingLease>> acquisition =
			session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish( Encoding.ASCII.GetBytes( response ) );

		await Assert.ThrowsAsync<FormatException>( () => acquisition );
		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public async Task MissingModeResponseRemainsTimeout() {
		AppearanceReportingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<TimeoutException>(
			() => session.AcquireAppearanceReportingAsync(
				TimeSpan.FromMilliseconds( 25 )
			).AsTask()
		);
		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public async Task PreCanceledAcquisitionWritesNothing() {
		AppearanceReportingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 ),
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task NonInteractiveEndpointRejectsAcquisitionWithoutOutput() {
		AppearanceReportingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			outputIsTerminal: false
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task SameValueReportsRemainDistinctSemanticEvents() {
		AppearanceReportingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalAppearanceReportingLease lease = await AcquireAsync(
			session,
			transport,
			1
		);

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?997;1n" ) );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?997;1n" ) );
		TerminalEvent first = await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) );
		TerminalEvent second = await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) );

		Assert.Equal( TerminalAppearance.Dark, first.Semantic?.Appearance?.Appearance );
		Assert.Equal( TerminalAppearance.Dark, second.Semantic?.Appearance?.Appearance );
		await lease.DisposeAsync();
		Assert.Equal( 1, transport.WriteCount );
	}

	private static async Task<TerminalAppearanceReportingLease> AcquireAsync(
		TerminalSession session,
		AppearanceReportingTransport transport,
		int state
	) {
		Task<TerminalControlResult<TerminalAppearanceReportingLease>> acquisition =
			session.AcquireAppearanceReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish( ModeResponse( state ) );
		return ( await acquisition ).GetRequiredValue();
	}

	private static byte[] ModeResponse(
		int state
	) {
		return Encoding.ASCII.GetBytes( $"\u001b[?2031;{state}$y" );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		AppearanceReportingTransport transport,
		bool outputIsTerminal = true
	) {
		return TerminalSession.OpenAsync(
			new AppearanceReportingControlProvider( outputIsTerminal ),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				RequireInteractiveOutput = outputIsTerminal,
				ObserveLifecycleEvents = false,
				InputDecoderOptions = new TerminalInputDecoderOptions {
					EscapeSequenceTimeout = TimeSpan.Zero
				}
			}
		);
	}

	private static async Task YieldSeveralTimesAsync() {
		for ( int count = 0; count < 32; ++count ) {
			await Task.Yield();
		}
	}

	private sealed class AppearanceReportingControlProvider(
		bool outputIsTerminal
	) : ITerminalControlProvider {
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
			bool isTerminal = !ReferenceEquals(
				endpoint,
				TerminalEndpoint.StandardOutput
			) || outputIsTerminal;
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isTerminal,
					null,
					isTerminal ? TerminalPlatformKind.PosixTermios : null,
					isTerminal
						? TerminalControlCapabilities.Attachment
							| TerminalControlCapabilities.ModeRead
							| TerminalControlCapabilities.ModeWrite
						: TerminalControlCapabilities.None
				)
			);
		}

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) {
			return TerminalControlResult<TerminalSize>.Available(
				new TerminalSize( 80, 24 )
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
		}

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) {
			return TerminalControlMutationResult.Success();
		}
	}

	private sealed class AppearanceReportingTransport : ITerminalInput, ITerminalOutput {
		private readonly object sync = new();
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly HashSet<int> failingWrites;
		private readonly List<byte[]> writes = [];
		private readonly SemaphoreSlim writeSignal = new( 0 );

		internal AppearanceReportingTransport(
			params int[] failingWrites
		) {
			this.failingWrites = [ .. failingWrites ];
		}

		internal int WriteCount {
			get {
				lock ( this.sync ) {
					return this.writes.Count;
				}
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
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel is closed."
				);
			}
		}

		internal async ValueTask WaitForWriteCountAsync(
			int expected
		) {
			using CancellationTokenSource timeout = new(
				TimeSpan.FromSeconds( 5 )
			);
			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}
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
			int writeNumber;
			lock ( this.sync ) {
				this.writes.Add( buffer.ToArray() );
				writeNumber = this.writes.Count;
			}
			this.writeSignal.Release();
			if ( this.failingWrites.Contains( writeNumber ) ) {
				throw new IOException( $"Synthetic output failure {writeNumber}." );
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
}
