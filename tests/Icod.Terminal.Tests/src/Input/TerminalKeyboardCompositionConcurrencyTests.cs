namespace Icod.Terminal.Tests.Input;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Verifies T175 atomic cross-manager keyboard/screen composition under concurrency and failure.
/// </summary>
public sealed class TerminalKeyboardCompositionConcurrencyTests {
	private const string ProbeRequest = "\u001b[?u\u001b[c";
	private const string PushAllKeys = "\u001b[>31u";
	private const string PopKeyboard = "\u001b[<u";
	private const string EnterAlternateScreen = "<A+>";
	private const string ExitAlternateScreen = "<A->";

	[Fact]
	public async Task ConcurrentKeyboardReleaseCannotInterleaveWithScreenHandoff() {
		ControlledTerminalTransport transport = new() {
			BlockOnValue = EnterAlternateScreen
		};
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalInputProtocolLease keyboard = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
				}
			)
		).GetRequiredValue();
		transport.ClearWrites();

		Task<TerminalControlResult<TerminalPresentationLease>> presentationTask =
			session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			).AsTask();
		await transport.WaitUntilBlockedAsync().WaitAsync(
			TimeSpan.FromSeconds( 5 )
		);

		Task releaseTask = keyboard.DisposeAsync().AsTask();
		Task first = await Task.WhenAny(
			releaseTask,
			Task.Delay( TimeSpan.FromMilliseconds( 100 ) )
		);
		Assert.NotSame( releaseTask, first );
		Assert.Equal( new[] { PopKeyboard }, transport.SuccessfulWrites );

		transport.ReleaseBlockedWrite();
		TerminalPresentationLease presentation = (
			await presentationTask.WaitAsync( TimeSpan.FromSeconds( 5 ) )
		).GetRequiredValue();
		await releaseTask.WaitAsync( TimeSpan.FromSeconds( 5 ) );

		Assert.Equal(
			new[] {
				PopKeyboard,
				EnterAlternateScreen,
				PushAllKeys,
				PopKeyboard
			},
			transport.SuccessfulWrites
		);

		transport.ClearWrites();
		await presentation.DisposeAsync();
		Assert.Equal( new[] { ExitAlternateScreen }, transport.SuccessfulWrites );
	}

	[Fact]
	public async Task FailedScreenSwitchRestoresKeyboardAndAllowsRetry() {
		ControlledTerminalTransport transport = new() {
			FailOnceOnValue = EnterAlternateScreen
		};
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalInputProtocolLease keyboard = (
			await session.AcquireInputProtocolsAsync(
				new TerminalInputProtocolOptions {
					KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
				}
			)
		).GetRequiredValue();
		transport.ClearWrites();

		await Assert.ThrowsAsync<IOException>(
			() => session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			).AsTask()
		);

		Assert.Equal(
			new[] {
				PopKeyboard,
				EnterAlternateScreen,
				PushAllKeys
			},
			transport.WriteAttempts
		);
		Assert.Equal(
			new[] {
				PopKeyboard,
				PushAllKeys
			},
			transport.SuccessfulWrites
		);

		transport.ClearWrites();
		TerminalPresentationLease retry = (
			await session.AcquirePresentationAsync(
				new TerminalPresentationOptions {
					AlternateScreen = true
				}
			)
		).GetRequiredValue();
		Assert.Equal(
			new[] {
				PopKeyboard,
				EnterAlternateScreen,
				PushAllKeys
			},
			transport.SuccessfulWrites
		);

		await retry.DisposeAsync();
		await keyboard.DisposeAsync();
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ControlledTerminalTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = new TerminalDescriptionBuilder( "kitty-concurrency-test" )
					.SetString(
						StringCapability.EnterCursorAddressingMode,
						EnterAlternateScreen
					)
					.SetString(
						StringCapability.ExitCursorAddressingMode,
						ExitAlternateScreen
					)
					.Build(),
				ConfigureOutput = false,
				ObserveLifecycleEvents = false
			}
		);
	}

	private sealed class ControlledTerminalTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
		private readonly TaskCompletionSource blocked = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private readonly TaskCompletionSource release = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);
		private bool failureUsed;

		internal string? BlockOnValue {
			get;
			init;
		}

		internal string? FailOnceOnValue {
			get;
			init;
		}

		internal List<string> WriteAttempts {
			get;
		} = [];

		internal List<string> SuccessfulWrites {
			get;
		} = [];

		internal void ClearWrites() {
			this.WriteAttempts.Clear();
			this.SuccessfulWrites.Clear();
		}

		internal Task WaitUntilBlockedAsync() {
			return this.blocked.Task;
		}

		internal void ReleaseBlockedWrite() {
			this.release.TrySetResult();
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] value = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			value.AsSpan().CopyTo( buffer.Span );
			return value.Length;
		}

		public async ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			string value = Encoding.Latin1.GetString( buffer.Span );
			this.WriteAttempts.Add( value );

			if ( string.Equals( this.BlockOnValue, value, StringComparison.Ordinal ) ) {
				this.blocked.TrySetResult();
				await this.release.Task.WaitAsync( cancellationToken ).ConfigureAwait( false );
			}

			if ( !this.failureUsed
				&& string.Equals(
					this.FailOnceOnValue,
					value,
					StringComparison.Ordinal
				) ) {
				this.failureUsed = true;
				throw new IOException( "Injected screen-switch output failure." );
			}

			this.SuccessfulWrites.Add( value );
			if ( string.Equals( ProbeRequest, value, StringComparison.Ordinal ) ) {
				byte[] response = Encoding.ASCII.GetBytes(
					"\u001b[?0u\u001b[?1;2c"
				);
				if ( !this.input.Writer.TryWrite( response ) ) {
					throw new InvalidOperationException(
						"The scripted Kitty probe response could not be queued."
					);
				}
			}
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
				"Live size is not required by keyboard composition tests."
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
