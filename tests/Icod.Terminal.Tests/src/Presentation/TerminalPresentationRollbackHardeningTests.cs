namespace Icod.Terminal.Tests.Presentation;

using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies that presentation-transition rollback failure preserves uncertainty
/// and does not retain ghost ownership.
/// </summary>
public sealed class TerminalPresentationRollbackHardeningTests {
	private const string EnterAlternateScreen = "<A+>";
	private const string ExitAlternateScreen = "<A->";
	private const string EnterKeypad = "<K+>";
	private const string ExitKeypad = "<K->";

	[Fact]
	public async Task FailedTransitionAndFailedRollbackLeaveNoGhostLeaseAndAllowRecovery() {
		FailureOutput output = new(
			EnterKeypad,
			ExitAlternateScreen
		);
		await using TerminalSession session = await OpenSessionAsync( output );

		AggregateException failure = await Assert.ThrowsAsync<AggregateException>(
			() => session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true,
					KeypadMode = true
				}
			).AsTask()
		);

		Assert.Contains(
			"presentation transition failed and rollback also reported an error",
			failure.Message,
			StringComparison.OrdinalIgnoreCase
		);
		Assert.Equal( 2, failure.InnerExceptions.Count );
		Assert.Equal(
			new[] {
				EnterAlternateScreen,
				EnterKeypad,
				ExitAlternateScreen
			},
			output.WriteAttempts
		);
		Assert.Equal(
			new[] {
				EnterAlternateScreen
			},
			output.SuccessfulWrites
		);

		output.ClearFailuresAndWrites();
		TerminalPresentationLease recovery = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			)
		).GetRequiredValue();

		Assert.Equal(
			new[] {
				EnterAlternateScreen
			},
			output.SuccessfulWrites
		);

		output.ClearWrites();
		await recovery.DisposeAsync();
		Assert.Equal(
			new[] {
				ExitAlternateScreen
			},
			output.SuccessfulWrites
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		FailureOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );

		TerminalDescription terminal = new TerminalDescriptionBuilder(
			"presentation-rollback-hardening"
		)
			.SetString(
				StringCapability.EnterCursorAddressingMode,
				EnterAlternateScreen
			)
			.SetString(
				StringCapability.ExitCursorAddressingMode,
				ExitAlternateScreen
			)
			.SetString(
				StringCapability.EnterKeypadMode,
				EnterKeypad
			)
			.SetString(
				StringCapability.ExitKeypadMode,
				ExitKeypad
			)
			.Build();

		return TerminalSession.OpenAsync(
			new TestTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class TestInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class FailureOutput : ITerminalOutput {
		private readonly HashSet<string> failingValues;

		internal FailureOutput(
			params string[] failingValues
		) {
			ArgumentNullException.ThrowIfNull( failingValues );
			this.failingValues = [ .. failingValues ];
		}

		internal List<string> WriteAttempts {
			get;
		} = [];

		internal List<string> SuccessfulWrites {
			get;
		} = [];

		internal void ClearFailuresAndWrites() {
			this.failingValues.Clear();
			this.ClearWrites();
		}

		internal void ClearWrites() {
			this.WriteAttempts.Clear();
			this.SuccessfulWrites.Clear();
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			string value = Encoding.Latin1.GetString( buffer.Span );
			this.WriteAttempts.Add( value );

			if ( this.failingValues.Remove( value ) ) {
				throw new IOException(
					$"Injected presentation failure for '{value}'."
				);
			}

			this.SuccessfulWrites.Add( value );
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
				"Live size is not required by presentation rollback hardening."
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
