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
/// Freezes the public one-shot Terminal 1.27 appearance query contract.
/// </summary>
public sealed class TerminalAppearanceQueryTests {
	[Theory]
	[InlineData( 1, TerminalAppearance.Dark )]
	[InlineData( 2, TerminalAppearance.Light )]
	public async Task QueryReturnsTypedAppearanceWithoutEnablingReporting(
		int wireValue,
		TerminalAppearance expected
	) {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<TerminalAppearance> query = session.QueryAppearanceAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b[?996n" ),
			transport.GetWrite( 0 )
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( $"\u001b[?997;{wireValue}n" )
		);

		Assert.Equal( expected, await query );
		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public async Task QueryAcceptsZeroTimeout() {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<TimeoutException>(
			() => session.QueryAppearanceAsync( TimeSpan.Zero ).AsTask()
		);
	}

	[Fact]
	public async Task QueryAcceptsOneMinuteTimeout() {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalAppearance> query = session.QueryAppearanceAsync(
			TimeSpan.FromMinutes( 1 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?997;1n" ) );
		Assert.Equal( TerminalAppearance.Dark, await query );
	}

	[Theory]
	[InlineData( -1 )]
	[InlineData( 60001 )]
	public async Task QueryRejectsTimeoutOutsideSupportedRange(
		int milliseconds
	) {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.QueryAppearanceAsync(
				TimeSpan.FromMilliseconds( milliseconds )
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task PreCanceledQueryWritesNoBytes() {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.QueryAppearanceAsync(
				TimeSpan.FromSeconds( 30 ),
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task NonInteractiveEndpointRejectsQueryWithoutOutput() {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			outputIsTerminal: false
		);

		await Assert.ThrowsAsync<InvalidOperationException>(
			() => session.QueryAppearanceAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
	}

	[Fact]
	public async Task MissingReplyRemainsTimeoutRatherThanUnknown() {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<TimeoutException>(
			() => session.QueryAppearanceAsync(
				TimeSpan.FromMilliseconds( 25 )
			).AsTask()
		);
	}

	[Theory]
	[InlineData( "\u001b[?997;0n" )]
	[InlineData( "\u001b[?997;3n" )]
	[InlineData( "\u001b[?997;1:2n" )]
	public async Task CorrelatedMalformedReplyFailsWithoutRetry(
		string response
	) {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalAppearance> query = session.QueryAppearanceAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		transport.Publish( Encoding.ASCII.GetBytes( response ) );

		await Assert.ThrowsAsync<FormatException>( () => query );
		Assert.Equal( 1, transport.WriteCount );
	}

	[Fact]
	public async Task ActiveQueryOwnsFirstReportAndLaterReportBecomesSemantic() {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalAppearance> query = session.QueryAppearanceAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?997;2n" ) );
		Assert.Equal( TerminalAppearance.Light, await query );
		Assert.Equal(
			TerminalEventKind.Timeout,
			( await session.ReadEventAsync( TimeSpan.Zero ) ).Kind
		);

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?997;1n" ) );
		TerminalEvent terminalEvent = await session.ReadEventAsync(
			TimeSpan.FromSeconds( 5 )
		);
		Assert.Equal( TerminalEventKind.Semantic, terminalEvent.Kind );
		Assert.Equal(
			TerminalAppearance.Dark,
			terminalEvent.Semantic?.Appearance?.Appearance
		);
	}

	[Fact]
	public async Task LateReportCannotCompleteNextAmbiguousQueryOrBecomeInput() {
		AppearanceTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalAppearance> first = session.QueryAppearanceAsync(
			TimeSpan.FromMilliseconds( 25 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		await Assert.ThrowsAsync<TimeoutException>( () => first );

		Task<TerminalAppearance> second = session.QueryAppearanceAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await YieldSeveralTimesAsync();
		Assert.Equal( 1, transport.WriteCount );

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?997;1n" ) );
		await transport.WaitForWriteCountAsync( 2 );
		Assert.False( second.IsCompleted );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?997;2n" ) );
		Assert.Equal( TerminalAppearance.Light, await second );

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?997;1n" ) );
		TerminalEvent later = await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) );
		Assert.Equal( TerminalEventKind.Semantic, later.Kind );
		Assert.Equal( TerminalAppearance.Dark, later.Semantic?.Appearance?.Appearance );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		AppearanceTransport transport,
		bool outputIsTerminal = true
	) {
		return TerminalSession.OpenAsync(
			new AppearanceControlProvider( outputIsTerminal ),
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

	private sealed class AppearanceControlProvider(
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

	private sealed class AppearanceTransport : ITerminalInput, ITerminalOutput {
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
}
