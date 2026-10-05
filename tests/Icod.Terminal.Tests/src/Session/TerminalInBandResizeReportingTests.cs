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
/// Freezes independently owned in-band resize reporting and observation provenance.
/// </summary>
public sealed class TerminalInBandResizeReportingTests {
	private static readonly byte[] ModeQuery =
		Encoding.ASCII.GetBytes( "\u001b[?2048$p" );
	private static readonly byte[] ModeEnable =
		Encoding.ASCII.GetBytes( "\u001b[?2048h" );
	private static readonly byte[] ModeDisable =
		Encoding.ASCII.GetBytes( "\u001b[?2048l" );

	[Theory]
	[InlineData( 0, false, false )]
	[InlineData( 1, true, false )]
	[InlineData( 2, true, true )]
	[InlineData( 3, true, false )]
	[InlineData( 4, false, false )]
	public async Task ModeStateControlsAvailabilityAndExactRestoration(
		int state,
		bool available,
		bool disables
	) {
		ResizeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalControlResult<TerminalInBandResizeReportingLease>> acquisition =
			session.AcquireInBandResizeReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		Assert.Equal( ModeQuery, transport.GetWrite( 0 ) );
		transport.Publish( ModeResponse( state ) );

		TerminalControlResult<TerminalInBandResizeReportingLease> result =
			await acquisition;
		Assert.Equal( available, result.IsAvailable );
		if ( !available ) {
			Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
			Assert.Equal( 1, transport.WriteCount );
			return;
		}

		TerminalInBandResizeReportingLease lease = result.GetRequiredValue();
		Assert.Equal( 2, transport.WriteCount );
		Assert.Equal( ModeEnable, transport.GetWrite( 1 ) );
		await lease.DisposeAsync();
		Assert.Equal( disables ? 3 : 2, transport.WriteCount );
		if ( disables ) {
			Assert.Equal( ModeDisable, transport.GetWrite( 2 ) );
		}
	}

	[Fact]
	public async Task NestedOwnersShareModeAndReleaseOutOfOrder() {
		ResizeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalInBandResizeReportingLease first = await AcquireAsync(
			session,
			transport,
			2
		);
		TerminalInBandResizeReportingLease second =
			( await session.AcquireInBandResizeReportingAsync(
				TimeSpan.FromSeconds( 30 )
			) ).GetRequiredValue();

		Assert.True( second.OwnerId > first.OwnerId );
		Assert.Equal( 2, transport.WriteCount );
		await first.DisposeAsync();
		Assert.Equal( 2, transport.WriteCount );
		await second.DisposeAsync();
		await second.DisposeAsync();
		Assert.Equal( 3, transport.WriteCount );
		Assert.Equal( ModeDisable, transport.GetWrite( 2 ) );
	}

	[Fact]
	public async Task ReportsRemainDistinctSemanticObservationsWithoutLifecycleSource() {
		ResizeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalInBandResizeReportingLease lease = await AcquireAsync(
			session,
			transport,
			1
		);
		Assert.False( session.SupportsLifecycleEvents );

		string[] reports = [
			"\u001b[48;24;80;600;800t",
			"\u001b[48;30;100;600;800t",
			"\u001b[48;30;100;900;1200t",
			"\u001b[48;30;100;900;1200t",
			"\u001b[48;40;120;0;0t"
		];
		foreach ( string report in reports ) {
			transport.Publish( Encoding.ASCII.GetBytes( report ) );
		}

		TerminalInBandResizeEvent[] observations = new TerminalInBandResizeEvent[ 5 ];
		for ( int index = 0; index < observations.Length; ++index ) {
			TerminalEvent terminalEvent = await session.ReadEventAsync(
				TimeSpan.FromSeconds( 5 )
			);
			Assert.Equal( TerminalEventKind.Semantic, terminalEvent.Kind );
			observations[ index ] = Assert.IsType<TerminalInBandResizeEvent>(
				terminalEvent.Semantic?.InBandResize
			);
		}

		Assert.Equal( new TerminalDimensions( 80, 24 ), observations[ 0 ].Dimensions );
		Assert.Equal( new TerminalDimensions( 100, 30 ), observations[ 1 ].Dimensions );
		Assert.Equal( new TerminalPixelDimensions( 1200, 900 ), observations[ 2 ].PixelDimensions );
		Assert.Equal( observations[ 2 ].Dimensions, observations[ 3 ].Dimensions );
		Assert.Equal( observations[ 2 ].PixelDimensions, observations[ 3 ].PixelDimensions );
		Assert.Null( observations[ 4 ].PixelDimensions );
		await Assert.ThrowsAsync<NotSupportedException>(
			() => session.ReadLifecycleEventAsync().AsTask()
		);
		await lease.DisposeAsync();
	}

	[Fact]
	public async Task InBandObservationDoesNotReplaceNativeGeometry() {
		ResizeTransport transport = new();
		ResizeControlProvider controlProvider = new() {
			Size = new TerminalSize( 80, 24 )
		};
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			controlProvider
		);
		TerminalInBandResizeReportingLease lease = await AcquireAsync(
			session,
			transport,
			1
		);

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[48;30;100;900;1200t" ) );
		TerminalInBandResizeEvent inBand = Assert.IsType<TerminalInBandResizeEvent>(
			( await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) ) )
				.Semantic?.InBandResize
		);
		Assert.Equal( new TerminalDimensions( 100, 30 ), inBand.Dimensions );
		Assert.Equal( new TerminalSize( 80, 24 ), session.GetSize().GetRequiredValue() );
		Assert.Equal(
			new TerminalDimensions( 80, 24 ),
			session.GetDimensions().GetRequiredValue()
		);
		await lease.DisposeAsync();
	}

	[Fact]
	public async Task ResizeAndAppearanceOwnershipRemainIndependent() {
		ResizeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Task<TerminalControlResult<TerminalAppearanceReportingLease>> appearanceTask =
			session.AcquireAppearanceReportingAsync( TimeSpan.FromSeconds( 30 ) ).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b[?2031;2$y" ) );
		TerminalAppearanceReportingLease appearance =
			( await appearanceTask ).GetRequiredValue();

		Task<TerminalControlResult<TerminalInBandResizeReportingLease>> resizeTask =
			session.AcquireInBandResizeReportingAsync( TimeSpan.FromSeconds( 30 ) ).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish( ModeResponse( 2 ) );
		TerminalInBandResizeReportingLease resize =
			( await resizeTask ).GetRequiredValue();

		await appearance.DisposeAsync();
		Assert.Equal( Encoding.ASCII.GetBytes( "\u001b[?2031l" ), transport.GetWrite( 4 ) );
		await resize.DisposeAsync();
		Assert.Equal( ModeDisable, transport.GetWrite( 5 ) );
	}

	[Fact]
	public async Task PreCanceledAcquisitionWritesNothing() {
		ResizeTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => session.AcquireInBandResizeReportingAsync(
				TimeSpan.FromSeconds( 30 ),
				cancellation.Token
			).AsTask()
		);
		Assert.Equal( 0, transport.WriteCount );
	}

	private static async Task<TerminalInBandResizeReportingLease> AcquireAsync(
		TerminalSession session,
		ResizeTransport transport,
		int state
	) {
		Task<TerminalControlResult<TerminalInBandResizeReportingLease>> acquisition =
			session.AcquireInBandResizeReportingAsync(
				TimeSpan.FromSeconds( 30 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		transport.Publish( ModeResponse( state ) );
		return ( await acquisition ).GetRequiredValue();
	}

	private static byte[] ModeResponse(
		int state
	) => Encoding.ASCII.GetBytes( $"\u001b[?2048;{state}$y" );

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ResizeTransport transport,
		ResizeControlProvider? controlProvider = null
	) => TerminalSession.OpenAsync(
		controlProvider ?? new ResizeControlProvider(),
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

	private sealed class ResizeControlProvider : ITerminalControlProvider {
		private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
			0, 0, 0, 0x0002UL, new byte[ 32 ], 0, 32, 0,
			new TerminalSpeed( 13, 9600 ), new TerminalSpeed( 13, 9600 )
		);

		internal TerminalSize Size {
			get;
			init;
		} = new( 80, 24 );

		public TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) => TerminalControlResult<TerminalEndpointObservation>.Available(
			new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.PosixTermios,
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
					| TerminalControlCapabilities.LiveSize
			)
		);

		public TerminalControlResult<TerminalSize> GetSize(
			TerminalEndpoint endpoint
		) => TerminalControlResult<TerminalSize>.Available( this.Size );

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) => TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );

		public TerminalControlMutationResult SetMode(
			TerminalEndpoint endpoint,
			TerminalModeSnapshot mode,
			TerminalModeApplyTiming timing
		) => TerminalControlMutationResult.Success();
	}

	private sealed class ResizeTransport : ITerminalInput, ITerminalOutput {
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

		internal byte[] GetWrite(
			int index
		) {
			lock ( this.sync ) {
				return this.writes[ index ].ToArray();
			}
		}

		internal void Publish(
			byte[] bytes
		) => Assert.True( this.input.Writer.TryWrite( bytes.ToArray() ) );

		internal async ValueTask WaitForWriteCountAsync(
			int expected
		) {
			using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
			while ( true ) {
				lock ( this.sync ) {
					if ( expected <= this.writes.Count ) {
						return;
					}
				}
				await this.writeSignal.WaitAsync( timeout.Token ).ConfigureAwait( false );
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] bytes = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
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
