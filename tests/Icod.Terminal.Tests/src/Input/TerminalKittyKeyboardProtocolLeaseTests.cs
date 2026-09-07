namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T173 Kitty keyboard support detection and reversible lease ownership.
/// </summary>
public sealed class TerminalKittyKeyboardProtocolLeaseTests {
	private const string ProbeRequest = "\u001b[?u\u001b[c";
	private const string PushDisambiguated = "\u001b[>5u";
	private const string PushEventTypes = "\u001b[>7u";
	private const string PushAllKeys = "\u001b[>31u";
	private const string PopKeyboard = "\u001b[<u";

	[Fact]
	public async Task SupportedProbeAndDisambiguatedLeaseUseExactFrames() {
		DuplexTerminalTransport transport = new(
			supportsKittyKeyboard: true,
			coalesceProbeResponses: true
		);
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalControlResult<TerminalInputProtocolLease> result =
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.Disambiguated
				}
			);
		TerminalInputProtocolLease lease = result.GetRequiredValue();

		Assert.Equal(
			TerminalKeyboardReportingMode.Disambiguated,
			lease.KeyboardReportingMode
		);
		Assert.Equal(
			new[] {
				ProbeRequest,
				PushDisambiguated
			},
			transport.SuccessfulWrites
		);

		transport.ClearWrites();
		await lease.DisposeAsync();

		Assert.Equal(
			new[] {
				PopKeyboard
			},
			transport.SuccessfulWrites
		);
	}

	[Fact]
	public async Task FragmentedProbeResponseAlsoEstablishesSupport() {
		DuplexTerminalTransport transport = new(
			supportsKittyKeyboard: true,
			coalesceProbeResponses: false
		);
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalInputProtocolLease lease = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.EventTypes
				}
			)
		).GetRequiredValue();

		Assert.Equal(
			new[] {
				ProbeRequest,
				PushEventTypes
			},
			transport.SuccessfulWrites
		);

		await lease.DisposeAsync();
	}

	[Fact]
	public async Task MissingKittyFlagsReplyReturnsUnavailableWithoutPush() {
		DuplexTerminalTransport transport = new(
			supportsKittyKeyboard: false,
			coalesceProbeResponses: true
		);
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalControlResult<TerminalInputProtocolLease> result =
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
				}
			);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal(
			new[] {
				ProbeRequest
			},
			transport.SuccessfulWrites
		);
		Assert.DoesNotContain( PushAllKeys, transport.SuccessfulWrites );
	}

	[Fact]
	public async Task NestedKeyboardLeasesUseStrongestRequestAndDowngradeExactly() {
		DuplexTerminalTransport transport = new(
			supportsKittyKeyboard: true,
			coalesceProbeResponses: true
		);
		await using TerminalSession session = await OpenSessionAsync( transport );

		TerminalInputProtocolLease outer = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.Disambiguated
				}
			)
		).GetRequiredValue();
		TerminalInputProtocolLease middle = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.EventTypes
				}
			)
		).GetRequiredValue();
		TerminalInputProtocolLease inner = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
				}
			)
		).GetRequiredValue();

		Assert.Equal(
			new[] {
				ProbeRequest,
				PushDisambiguated,
				ProbeRequest,
				PopKeyboard,
				PushEventTypes,
				ProbeRequest,
				PopKeyboard,
				PushAllKeys
			},
			transport.SuccessfulWrites
		);

		transport.ClearWrites();
		await middle.DisposeAsync();
		Assert.Empty( transport.SuccessfulWrites );

		await inner.DisposeAsync();
		Assert.Equal(
			new[] {
				PopKeyboard,
				PushDisambiguated
			},
			transport.SuccessfulWrites
		);

		transport.ClearWrites();
		await outer.DisposeAsync();
		Assert.Equal(
			new[] {
				PopKeyboard
			},
			transport.SuccessfulWrites
		);
	}

	[Fact]
	public async Task KeyboardAndExistingRichInputProtocolsShareOneLease() {
		DuplexTerminalTransport transport = new(
			supportsKittyKeyboard: true,
			coalesceProbeResponses: true
		);
		await using TerminalSession session = await OpenSessionAsync(
			transport,
			CreateRichInputTerminal()
		);

		TerminalInputProtocolLease lease = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					FocusReporting = true,
					MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents,
					KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
				}
			)
		).GetRequiredValue();

		Assert.Equal(
			new[] {
				ProbeRequest,
				"<P+>",
				"<F+>",
				"\u001b[?1006h",
				"\u001b[?1000h",
				PushAllKeys
			},
			transport.SuccessfulWrites
		);

		transport.ClearWrites();
		await lease.DisposeAsync();
		Assert.Equal(
			new[] {
				PopKeyboard,
				"\u001b[?1000l",
				"\u001b[?1006l",
				"<F->",
				"<P->"
			},
			transport.SuccessfulWrites
		);
	}

	[Fact]
	public async Task InvalidKeyboardReportingModeIsRejectedBeforeProbeOutput() {
		DuplexTerminalTransport transport = new(
			supportsKittyKeyboard: true,
			coalesceProbeResponses: true
		);
		await using TerminalSession session = await OpenSessionAsync( transport );

		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = (TerminalKeyboardReportingMode)int.MaxValue
				}
			).AsTask()
		);

		Assert.Empty( transport.SuccessfulWrites );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		DuplexTerminalTransport transport,
		TerminalDescription? terminal = null
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = terminal
					?? new TerminalDescriptionBuilder( "kitty-keyboard-test" ).Build(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private static TerminalDescription CreateRichInputTerminal() {
		return new TerminalDescriptionBuilder( "kitty-rich-input-test" )
			.SetExtendedString( "BE", "<P+>" )
			.SetExtendedString( "BD", "<P->" )
			.SetExtendedString( "PS", "\u001b[200~" )
			.SetExtendedString( "PE", "\u001b[201~" )
			.SetExtendedString( "fe", "<F+>" )
			.SetExtendedString( "fd", "<F->" )
			.SetExtendedString( "kxIN", "\u001b[I" )
			.SetExtendedString( "kxOUT", "\u001b[O" )
			.SetString( StringCapability.KeyMouse, "\u001b[<" )
			.SetExtendedString(
				"XM",
				"\u001b[?1006;1000%?%p1%{1}%=%th%el%;"
			)
			.SetExtendedString(
				"xm",
				"\u001b[<%i%p3%d;%p1%d;%p2%d;%?%p4%tM%em%;"
			)
			.Build();
	}

	private sealed class DuplexTerminalTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly bool supportsKittyKeyboard;
		private readonly bool coalesceProbeResponses;

		internal DuplexTerminalTransport(
			bool supportsKittyKeyboard,
			bool coalesceProbeResponses
		) {
			this.supportsKittyKeyboard = supportsKittyKeyboard;
			this.coalesceProbeResponses = coalesceProbeResponses;
		}

		internal List<string> SuccessfulWrites {
			get;
		} = [];

		internal void ClearWrites() {
			this.SuccessfulWrites.Clear();
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
			this.SuccessfulWrites.Add( value );
			if ( string.Equals( ProbeRequest, value, StringComparison.Ordinal ) ) {
				this.EnqueueProbeResponse();
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}

		private void EnqueueProbeResponse() {
			byte[] da = Encoding.ASCII.GetBytes( "\u001b[?1;2c" );
			if ( !this.supportsKittyKeyboard ) {
				Assert.True( this.input.Writer.TryWrite( da ) );
				return;
			}

			byte[] flags = Encoding.ASCII.GetBytes( "\u001b[?0u" );
			if ( this.coalesceProbeResponses ) {
				Assert.True(
					this.input.Writer.TryWrite(
						flags.Concat( da ).ToArray()
					)
				);
				return;
			}

			Assert.True( this.input.Writer.TryWrite( flags ) );
			Assert.True( this.input.Writer.TryWrite( da ) );
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
				"Live size is not required by Kitty keyboard lease tests."
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
