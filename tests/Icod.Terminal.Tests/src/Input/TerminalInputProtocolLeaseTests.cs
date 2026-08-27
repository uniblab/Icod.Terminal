namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T15 reversible rich-input protocol ownership without touching the process terminal.
/// </summary>
public sealed class TerminalInputProtocolLeaseTests {
	private const string SgrEnable = "\u001b[?1006h";
	private const string SgrDisable = "\u001b[?1006l";
	private const string ButtonEnable = "\u001b[?1000h";
	private const string ButtonDisable = "\u001b[?1000l";
	private const string ButtonMotionEnable = "\u001b[?1002h";
	private const string ButtonMotionDisable = "\u001b[?1002l";
	private const string AnyMotionEnable = "\u001b[?1003h";
	private const string AnyMotionDisable = "\u001b[?1003l";

	[Fact]
	public async Task CompoundProtocolLeaseEntersAndLeavesInDeterministicOrder() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreateRichInputTerminal(),
			output
		);

		TerminalControlResult<TerminalInputProtocolLease> result =
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					FocusReporting = true,
					MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents
				}
			);
		TerminalInputProtocolLease lease = result.GetRequiredValue();

		Assert.Equal(
			new[] {
				"<P+>",
				"<F+>",
				SgrEnable,
				ButtonEnable
			},
			output.SuccessfulWrites
		);
		Assert.Equal( 1, output.FlushCount );

		output.Clear();
		await lease.DisposeAsync();

		Assert.Equal(
			new[] {
				ButtonDisable,
				SgrDisable,
				"<F->",
				"<P->"
			},
			output.SuccessfulWrites
		);
		Assert.Equal( 1, output.FlushCount );

		await session.DisposeAsync();
	}

	[Fact]
	public async Task NestedMouseLeasesUseStrongestModeAndDowngradeDeterministically() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreateRichInputTerminal(),
			output
		);

		TerminalInputProtocolLease outer = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents
				}
			)
		).GetRequiredValue();
		TerminalInputProtocolLease middle = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					MouseTrackingMode = TerminalMouseTrackingMode.ButtonMotion
				}
			)
		).GetRequiredValue();
		TerminalInputProtocolLease inner = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					MouseTrackingMode = TerminalMouseTrackingMode.AnyMotion
				}
			)
		).GetRequiredValue();

		Assert.Equal(
			new[] {
				"<P+>",
				SgrEnable,
				ButtonEnable,
				ButtonDisable,
				ButtonMotionEnable,
				ButtonMotionDisable,
				AnyMotionEnable
			},
			output.SuccessfulWrites
		);

		output.Clear();
		await middle.DisposeAsync();
		Assert.Empty( output.SuccessfulWrites );

		await inner.DisposeAsync();
		Assert.Equal(
			new[] {
				AnyMotionDisable,
				ButtonEnable
			},
			output.SuccessfulWrites
		);

		output.Clear();
		await outer.DisposeAsync();
		Assert.Equal(
			new[] {
				ButtonDisable,
				SgrDisable,
				"<P->"
			},
			output.SuccessfulWrites
		);

		await session.DisposeAsync();
	}

	[Fact]
	public async Task MissingProtocolContractReturnsUnavailableWithoutOutput() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "incomplete-rich-input" )
			.SetExtendedString( "BE", "<P+>" )
			.Build();
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( terminal, output );

		TerminalControlResult<TerminalInputProtocolLease> result =
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true
				}
			);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Empty( output.WriteAttempts );
		Assert.Equal( 0, output.FlushCount );

		await session.DisposeAsync();
	}

	[Fact]
	public async Task PartialAcquisitionFailureRollsBackCompletedProtocolTransitions() {
		RecordingTerminalOutput output = new() {
			FailOnValue = "<F+>"
		};
		TerminalSession session = await OpenSessionAsync(
			CreateRichInputTerminal(),
			output
		);

		await Assert.ThrowsAsync<IOException>(
			() => session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					FocusReporting = true
				}
			).AsTask()
		);

		Assert.Equal(
			new[] {
				"<P+>",
				"<F+>",
				"<P->"
			},
			output.WriteAttempts
		);
		Assert.Equal(
			new[] {
				"<P+>",
				"<P->"
			},
			output.SuccessfulWrites
		);

		output.Clear();
		TerminalInputProtocolLease retry = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true
				}
			)
		).GetRequiredValue();
		Assert.Equal( new[] { "<P+>" }, output.SuccessfulWrites );

		await retry.DisposeAsync();
		await session.DisposeAsync();
	}

	[Fact]
	public async Task LegacyMouseProfileDoesNotEnableSgrEncoding() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreateRichInputTerminal( "\u001b[M" ),
			output
		);

		TerminalInputProtocolLease lease = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents
				}
			)
		).GetRequiredValue();

		Assert.Equal(
			new[] {
				ButtonEnable
			},
			output.SuccessfulWrites
		);

		output.Clear();
		await lease.DisposeAsync();
		Assert.Equal(
			new[] {
				ButtonDisable
			},
			output.SuccessfulWrites
		);

		await session.DisposeAsync();
	}

	[Fact]
	public async Task SessionDisposalRestoresUndisposedProtocolLease() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreateRichInputTerminal(),
			output
		);
		TerminalInputProtocolLease lease = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					FocusReporting = true,
					MouseTrackingMode = TerminalMouseTrackingMode.AnyMotion
				}
			)
		).GetRequiredValue();
		output.Clear();

		await session.DisposeAsync();

		Assert.Equal(
			new[] {
				AnyMotionDisable,
				SgrDisable,
				"<F->",
				"<P->"
			},
			output.SuccessfulWrites
		);

		output.Clear();
		await lease.DisposeAsync();
		Assert.Empty( output.SuccessfulWrites );
	}

	[Fact]
	public async Task SuspendResumeReentersActiveProtocolLease() {
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync(
			CreateRichInputTerminal(),
			output,
			lifecycle
		);
		TerminalInputProtocolLease lease = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					BracketedPaste = true,
					FocusReporting = true,
					MouseTrackingMode = TerminalMouseTrackingMode.AnyMotion
				}
			)
		).GetRequiredValue();
		output.Clear();
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync(
			timeout.Token
		);
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync(
			timeout.Token
		);

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal(
			new[] {
				AnyMotionDisable,
				SgrDisable,
				"<F->",
				"<P->",
				"<P+>",
				"<F+>",
				SgrEnable,
				AnyMotionEnable
			},
			output.SuccessfulWrites
		);

		await lease.DisposeAsync();
		await session.DisposeAsync();
	}

	[Fact]
	public async Task RejectsEmptyAndInvalidInputProtocolOptions() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreateRichInputTerminal(),
			output
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions()
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					MouseTrackingMode = (TerminalMouseTrackingMode)int.MaxValue
				}
			).AsTask()
		);
		Assert.Empty( output.WriteAttempts );

		await session.DisposeAsync();
	}

	private static TerminalDescription CreateRichInputTerminal(
		string keyMouse = "\u001b[<"
	) {
		ArgumentNullException.ThrowIfNull( keyMouse );

		return new TerminalDescriptionBuilder( "rich-input-test" )
			.SetExtendedString( "BE", "<P+>" )
			.SetExtendedString( "BD", "<P->" )
			.SetExtendedString( "PS", "\u001b[200~" )
			.SetExtendedString( "PE", "\u001b[201~" )
			.SetExtendedString( "fe", "<F+>" )
			.SetExtendedString( "fd", "<F->" )
			.SetExtendedString( "kxIN", "\u001b[I" )
			.SetExtendedString( "kxOUT", "\u001b[O" )
			.SetString( StringCapability.KeyMouse, keyMouse )
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

	private static ValueTask<TerminalSession> OpenSessionAsync(
		TerminalDescription terminal,
		RecordingTerminalOutput output,
		TestTerminalLifecycleSource? lifecycle = null
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				LifecycleSource = lifecycle
			}
		);
	}

	private sealed class TestTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingTerminalOutput : ITerminalOutput {
		private bool failureUsed;
		private string? failOnValue;

		internal string? FailOnValue {
			get {
				return this.failOnValue;
			}
			init {
				this.failOnValue = value;
				this.failureUsed = false;
			}
		}

		internal List<string> WriteAttempts {
			get;
		} = [];

		internal List<string> SuccessfulWrites {
			get;
		} = [];

		internal int FlushCount {
			get;
			private set;
		}

		internal void Clear() {
			this.WriteAttempts.Clear();
			this.SuccessfulWrites.Clear();
			this.FlushCount = 0;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			string value = Encoding.Latin1.GetString( buffer.Span );
			this.WriteAttempts.Add( value );

			if ( !this.failureUsed
				&& string.Equals(
					this.FailOnValue,
					value,
					StringComparison.Ordinal
				) ) {
				this.failureUsed = true;
				throw new IOException( "Injected input-protocol output failure." );
			}

			this.SuccessfulWrites.Add( value );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.FlushCount;
			return ValueTask.CompletedTask;
		}
	}

	private sealed class TestTerminalLifecycleSource
		: ITerminalLifecycleSource,
		  ITerminalSuspendController {
		private readonly Channel<TerminalLifecycleSignal> signals =
			Channel.CreateUnbounded<TerminalLifecycleSignal>();

		internal bool AutoResume {
			get;
			init;
		}

		internal void Publish(
			TerminalLifecycleSignalKind kind
		) {
			Assert.True(
				this.signals.Writer.TryWrite( new TerminalLifecycleSignal( kind ) )
			);
		}

		public ValueTask<TerminalLifecycleSignal> ReadAsync(
			CancellationToken cancellationToken = default
		) {
			return this.signals.Reader.ReadAsync( cancellationToken );
		}

		public TerminalControlMutationResult SuspendCurrentProcess() {
			if ( this.AutoResume ) {
				this.Publish( TerminalLifecycleSignalKind.Resume );
			}
			return TerminalControlMutationResult.Success();
		}

		public void Dispose() {
			this.signals.Writer.TryComplete();
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
				"Live size is not required by this input-protocol test."
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
