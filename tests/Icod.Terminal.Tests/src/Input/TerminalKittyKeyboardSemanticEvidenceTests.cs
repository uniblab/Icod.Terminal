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
/// Verifies N158 semantic evidence produced by the existing Kitty keyboard probe.
/// </summary>
public sealed class TerminalKittyKeyboardSemanticEvidenceTests {
	private const string ProbeRequest = "\u001b[?u\u001b[c";

	[Fact]
	public async Task KittyFlagsResponseVerifiesKeyboardBackend() {
		ProbeTransport transport = new( supportsKittyKeyboard: true );
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalControlResult<TerminalInputProtocolLease> result =
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.Disambiguated
				}
			);
		TerminalInputProtocolLease lease = result.GetRequiredValue();
		try {
			TerminalCapabilityResolution evidence = session.GetSemanticCapabilityEvidence().Resolve(
				TerminalCapabilitySubject.ForProtocolBackend(
					TerminalProtocolBackend.CsiKittyKeyboard
				)
			);
			TerminalSemanticBackendResolution routing = session.ResolveSemanticBackend(
				TerminalSemanticOperation.KeyboardReporting
			);

			Assert.Equal( TerminalCapabilitySupportState.Verified, evidence.State );
			Assert.Equal( TerminalCapabilityEvidenceSource.ProtocolResponse, evidence.EvidenceSource );
			Assert.NotNull( routing.SelectedCandidate );
			Assert.Equal(
				TerminalProtocolBackend.CsiKittyKeyboard,
				routing.SelectedCandidate.Value.Backend
			);
			Assert.Equal( TerminalBackendSelectionReason.Verified, routing.SelectionReason );
		} finally {
			await lease.DisposeAsync();
		}
	}

	[Fact]
	public async Task PrimaryDaBarrierWithoutKittyFlagsRecordsUnsupported() {
		ProbeTransport transport = new( supportsKittyKeyboard: false );
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalControlResult<TerminalInputProtocolLease> result =
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
				}
			);
		TerminalCapabilityResolution evidence = session.GetSemanticCapabilityEvidence().Resolve(
			TerminalCapabilitySubject.ForProtocolBackend(
				TerminalProtocolBackend.CsiKittyKeyboard
			)
		);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal( TerminalCapabilitySupportState.Unsupported, evidence.State );
		Assert.Equal( TerminalCapabilityEvidenceSource.ProtocolResponse, evidence.EvidenceSource );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ProbeTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = new TerminalDescriptionBuilder( "kitty-evidence-test" ).Build(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class ProbeTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly bool supportsKittyKeyboard;

		internal ProbeTransport(
			bool supportsKittyKeyboard
		) {
			this.supportsKittyKeyboard = supportsKittyKeyboard;
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
					"The scripted terminal response exceeds the decoder read buffer."
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
			string value = Encoding.Latin1.GetString( buffer.Span );
			if ( string.Equals( ProbeRequest, value, StringComparison.Ordinal ) ) {
				byte[] da = Encoding.ASCII.GetBytes( "\u001b[?1;2c" );
				if ( this.supportsKittyKeyboard ) {
					byte[] flags = Encoding.ASCII.GetBytes( "\u001b[?0u" );
					if ( !this.input.Writer.TryWrite( flags.Concat( da ).ToArray() ) ) {
						throw new InvalidOperationException(
							"The scripted terminal input channel is closed."
						);
					}
				} else if ( !this.input.Writer.TryWrite( da ) ) {
					throw new InvalidOperationException(
						"The scripted terminal input channel is closed."
					);
				}
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
			return TerminalControlResult<TerminalSize>.Unavailable(
				"Live size is not required by this test."
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
