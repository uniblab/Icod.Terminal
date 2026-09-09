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
/// Verifies N158 integration between real OSC 99 support queries and semantic evidence.
/// </summary>
public sealed class TerminalSessionKittyNotificationEvidenceTests {
	[Fact]
	public async Task SuccessfulSupportQueryVerifiesBackendUntilStateInvalidation() {
		QueryTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalSemanticBackendResolution before = session.ResolveSemanticBackend(
			TerminalSemanticOperation.DesktopNotification
		);
		Assert.NotNull( before.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.Osc9Notification,
			before.SelectedCandidate.Value.Backend
		);
		Assert.Equal( TerminalBackendSelectionReason.SafeFallback, before.SelectionReason );

		Task<KittyNotificationSupport> query = session.QueryKittyNotificationSupportAsync(
			TimeSpan.FromSeconds( 30 )
		).AsTask();
		await transport.WaitForWriteCountAsync( 1 );

		string request = Encoding.ASCII.GetString( transport.GetWrite( 0 ) );
		const string prefix = "\u001b]99;i=";
		const string suffix = ":p=?;\u001b\\";
		Assert.StartsWith( prefix, request, StringComparison.Ordinal );
		Assert.EndsWith( suffix, request, StringComparison.Ordinal );
		string identifier = request.Substring(
			prefix.Length,
			request.Length - prefix.Length - suffix.Length
		);
		Assert.NotEmpty( identifier );

		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b]99;i={identifier}:p=?;p=title,body\u001b\\"
			)
		);
		_ = await query;

		TerminalSemanticBackendResolution verified = session.ResolveSemanticBackend(
			TerminalSemanticOperation.DesktopNotification
		);
		Assert.NotNull( verified.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.Osc99KittyNotification,
			verified.SelectedCandidate.Value.Backend
		);
		Assert.Equal( TerminalCapabilitySupportState.Verified, verified.State );
		Assert.Equal( TerminalCapabilityEvidenceSource.ProtocolResponse, verified.EvidenceSource );
		Assert.Equal( TerminalBackendSelectionReason.Verified, verified.SelectionReason );

		session.InvalidateState();
		TerminalSemanticBackendResolution invalidated = session.ResolveSemanticBackend(
			TerminalSemanticOperation.DesktopNotification
		);
		Assert.NotNull( invalidated.SelectedCandidate );
		Assert.Equal(
			TerminalProtocolBackend.Osc9Notification,
			invalidated.SelectedCandidate.Value.Backend
		);
		Assert.Equal( TerminalBackendSelectionReason.SafeFallback, invalidated.SelectionReason );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		QueryTransport transport
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

	private sealed class QueryTransport : ITerminalInput, ITerminalOutput {
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
