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
/// Defines E194 authoritative-reader routing and query coexistence for unsolicited semantic events.
/// </summary>
public sealed class TerminalSessionSemanticEventRoutingTests {
	[Fact]
	public async Task FragmentedSemanticReportPreservesApplicationByteStreamOrder() {
		RoutingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		transport.Publish( Encoding.ASCII.GetBytes( "x\u001b]99;i=job" ) );
		transport.Publish( Encoding.ASCII.GetBytes( ";\u001b\\y" ) );

		TerminalEvent first = await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) );
		TerminalEvent second = await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) );
		TerminalEvent third = await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) );

		Assert.Equal( TerminalEventKind.Input, first.Kind );
		Assert.Equal( new Rune( 'x' ), first.Input?.Character );
		AssertSemanticActivation( second, "job" );
		Assert.Equal( TerminalEventKind.Input, third.Kind );
		Assert.Equal( new Rune( 'y' ), third.Input?.Character );
	}

	[Fact]
	public async Task ActiveSupportQueryOwnsResponseWhileUnrelatedReportRemainsSemantic() {
		RoutingTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		Task<KittyNotificationSupport> query = session.QueryKittyNotificationSupportAsync(
			TimeSpan.FromSeconds( 5 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );
		string identifier = GetSupportQueryIdentifier( transport.GetWrite( 0 ) );

		transport.Publish( Encoding.ASCII.GetBytes( "x" ) );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b]99;i=notice" ) );
		transport.Publish( Encoding.ASCII.GetBytes( ";\u001b\\" ) );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b]99;i={identifier}:p=?;p=title,body\u001b\\"
			)
		);
		transport.Publish( Encoding.ASCII.GetBytes( "y" ) );

		KittyNotificationSupport support = await query;
		Assert.True( support.SupportsTitle );
		Assert.True( support.SupportsBody );

		TerminalEvent first = await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) );
		TerminalEvent second = await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) );
		TerminalEvent third = await session.ReadEventAsync( TimeSpan.FromSeconds( 5 ) );

		Assert.Equal( TerminalEventKind.Input, first.Kind );
		Assert.Equal( new Rune( 'x' ), first.Input?.Character );
		AssertSemanticActivation( second, "notice" );
		Assert.Equal( TerminalEventKind.Input, third.Kind );
		Assert.Equal( new Rune( 'y' ), third.Input?.Character );

		TerminalEvent trailing = await session.ReadEventAsync( TimeSpan.Zero );
		Assert.Equal( TerminalEventKind.Timeout, trailing.Kind );
	}

	private static void AssertSemanticActivation(
		TerminalEvent terminalEvent,
		string identifier
	) {
		ArgumentNullException.ThrowIfNull( terminalEvent );
		ArgumentException.ThrowIfNullOrEmpty( identifier );

		Assert.Equal( TerminalEventKind.Semantic, terminalEvent.Kind );
		Assert.Null( terminalEvent.Input );
		TerminalSemanticEvent semantic = Assert.IsType<TerminalSemanticEvent>(
			terminalEvent.Semantic
		);
		TerminalNotificationEvent notification = Assert.IsType<TerminalNotificationEvent>(
			semantic.Notification
		);
		Assert.Equal( TerminalNotificationEventKind.Activated, notification.Kind );
		Assert.Equal( identifier, notification.Identifier );
	}

	private static string GetSupportQueryIdentifier(
		byte[] requestBytes
	) {
		ArgumentNullException.ThrowIfNull( requestBytes );
		string request = Encoding.ASCII.GetString( requestBytes );
		const string prefix = "\u001b]99;i=";
		const string suffix = ":p=?;\u001b\\";
		Assert.StartsWith( prefix, request, StringComparison.Ordinal );
		Assert.EndsWith( suffix, request, StringComparison.Ordinal );
		return request.Substring(
			prefix.Length,
			request.Length - prefix.Length - suffix.Length
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		RoutingTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
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

	private sealed class RoutingTransport : ITerminalInput, ITerminalOutput {
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
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new();
			timeout.CancelAfter( TimeSpan.FromSeconds( 5 ) );
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
			if ( buffer.IsEmpty ) {
				return 0;
			}

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

	private sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
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
				"Size is not required by this test."
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
