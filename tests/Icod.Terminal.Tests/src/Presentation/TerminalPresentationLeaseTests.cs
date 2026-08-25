namespace Icod.Terminal.Tests.Presentation;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T09 reversible presentation ownership without touching the process terminal.
/// </summary>
public sealed class TerminalPresentationLeaseTests {
	/// <summary>Verifies grouped acquisition enters and leaves presentation state in deterministic order.</summary>
	[Fact]
	public async Task CompoundPresentationEntersAndLeavesInOrder() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			output
		);

		TerminalControlResult<TerminalPresentationLease> result =
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true,
					KeypadMode = true,
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			);
		TerminalPresentationLease lease = result.GetRequiredValue();

		Assert.Equal(
			new[] { "<A+>", "<K+>", "<C0>" },
			output.SuccessfulWrites
		);
		Assert.Equal( 1, output.FlushCount );

		output.Clear();
		await lease.DisposeAsync();

		Assert.Equal(
			new[] { "<C1>", "<K->", "<A->" },
			output.SuccessfulWrites
		);
		Assert.Equal( 1, output.FlushCount );

		await session.DisposeAsync();
	}

	/// <summary>
	/// Verifies first-owner/last-owner semantics survive out-of-order disposal.
	/// </summary>
	[Fact]
	public async Task NestedAlternateScreenUsesFirstOwnerLastOwner() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			output
		);

		TerminalPresentationLease outer = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			)
		).GetRequiredValue();
		TerminalPresentationLease inner = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			)
		).GetRequiredValue();

		Assert.Equal( new[] { "<A+>" }, output.SuccessfulWrites );
		Assert.Equal( 1, output.FlushCount );

		await outer.DisposeAsync();
		Assert.Equal( new[] { "<A+>" }, output.SuccessfulWrites );

		await inner.DisposeAsync();
		Assert.Equal( new[] { "<A+>", "<A->" }, output.SuccessfulWrites );
		Assert.Equal( 2, output.FlushCount );

		await session.DisposeAsync();
	}

	/// <summary>
	/// Verifies an inner cursor request temporarily overrides and then restores an outer request.
	/// </summary>
	[Fact]
	public async Task NestedCursorLeaseRestoresOuterVisibility() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			output
		);

		TerminalPresentationLease hidden = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			)
		).GetRequiredValue();
		TerminalPresentationLease normal = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					CursorVisibility = TerminalCursorVisibility.Normal
				}
			)
		).GetRequiredValue();

		await normal.DisposeAsync();
		await hidden.DisposeAsync();

		Assert.Equal(
			new[] { "<C0>", "<C1>", "<C0>", "<C1>" },
			output.SuccessfulWrites
		);

		await session.DisposeAsync();
	}

	/// <summary>Verifies missing TermInfo transitions are controlled rather than guessed.</summary>
	[Fact]
	public async Task MissingCapabilityReturnsUnavailableWithoutAnsiFallback() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "incomplete" )
			.SetString(
				StringCapability.EnterCursorAddressingMode,
				"<A+>"
			)
			.Build();
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync( terminal, output );

		TerminalControlResult<TerminalPresentationLease> result =
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Empty( output.WriteAttempts );
		Assert.Equal( 0, output.FlushCount );

		await session.DisposeAsync();
	}

	/// <summary>
	/// Verifies a failed later transition reverses transitions already completed by the acquisition.
	/// </summary>
	[Fact]
	public async Task PartialAcquisitionFailureRollsBackCompletedTransitions() {
		RecordingTerminalOutput output = new() {
			FailOnValue = "<K+>"
		};
		TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			output
		);

		await Assert.ThrowsAsync<IOException>(
			() => session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true,
					KeypadMode = true
				}
			).AsTask()
		);

		Assert.Equal(
			new[] { "<A+>", "<K+>", "<A->" },
			output.WriteAttempts
		);
		Assert.Equal(
			new[] { "<A+>", "<A->" },
			output.SuccessfulWrites
		);

		output.FailOnValue = null;
		output.Clear();
		TerminalPresentationLease retry = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			)
		).GetRequiredValue();
		Assert.Equal( new[] { "<A+>" }, output.SuccessfulWrites );

		await retry.DisposeAsync();
		await session.DisposeAsync();
	}

	/// <summary>
	/// Verifies session disposal restores presentation state even when a lease owner does not dispose first.
	/// </summary>
	[Fact]
	public async Task SessionDisposalRestoresUndisposedLease() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			output
		);
		TerminalPresentationLease lease = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true,
					KeypadMode = true,
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			)
		).GetRequiredValue();
		output.Clear();

		await session.DisposeAsync();

		Assert.Equal(
			new[] { "<C1>", "<K->", "<A->" },
			output.SuccessfulWrites
		);

		output.Clear();
		await lease.DisposeAsync();
		Assert.Empty( output.SuccessfulWrites );
	}

	/// <summary>
	/// Verifies T07 suspension releases active presentation state and resume re-enters it.
	/// </summary>
	[Fact]
	public async Task SuspendResumeReentersActivePresentationLease() {
		RecordingTerminalOutput output = new();
		TestTerminalLifecycleSource lifecycle = new() {
			AutoResume = true
		};
		TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			output,
			lifecycle
		);
		TerminalPresentationLease lease = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true,
					KeypadMode = true,
					CursorVisibility = TerminalCursorVisibility.Hidden
				}
			)
		).GetRequiredValue();
		output.Clear();
		using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );

		lifecycle.Publish( TerminalLifecycleSignalKind.Suspend );
		TerminalLifecycleEvent suspending = await session.ReadLifecycleEventAsync( timeout.Token );
		TerminalLifecycleEvent resumed = await session.ReadLifecycleEventAsync( timeout.Token );

		Assert.Equal( TerminalLifecycleEventKind.Suspending, suspending.Kind );
		Assert.Equal( TerminalLifecycleEventKind.Resumed, resumed.Kind );
		Assert.Equal(
			new[] {
				"<C1>",
				"<K->",
				"<A->",
				"<A+>",
				"<K+>",
				"<C0>"
			},
			output.SuccessfulWrites
		);

		await lease.DisposeAsync();
		await session.DisposeAsync();
	}

	/// <summary>Verifies empty and invalid presentation requests fail before output.</summary>
	[Fact]
	public async Task RejectsInvalidPresentationOptions() {
		RecordingTerminalOutput output = new();
		TerminalSession session = await OpenSessionAsync(
			CreatePresentationTerminal(),
			output
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => session.AcquirePresentationAsync(
				new TerminalPresentationOptions()
			).AsTask()
		);
		await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
			() => session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					CursorVisibility = (TerminalCursorVisibility)int.MaxValue
				}
			).AsTask()
		);
		Assert.Empty( output.WriteAttempts );

		await session.DisposeAsync();
	}

	private static TerminalDescription CreatePresentationTerminal() {
		return new TerminalDescriptionBuilder( "presentation-test" )
			.SetString(
				StringCapability.EnterCursorAddressingMode,
				"<A+>"
			)
			.SetString(
				StringCapability.ExitCursorAddressingMode,
				"<A->"
			)
			.SetString(
				StringCapability.EnterKeypadMode,
				"<K+>"
			)
			.SetString(
				StringCapability.ExitKeypadMode,
				"<K->"
			)
			.SetString(
				StringCapability.CursorInvisible,
				"<C0>"
			)
			.SetString(
				StringCapability.CursorNormal,
				"<C1>"
			)
			.SetString(
				StringCapability.CursorVeryVisible,
				"<C2>"
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
			set {
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
				throw new IOException( "Injected presentation output failure." );
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
				"Live size is not required by this presentation test."
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
