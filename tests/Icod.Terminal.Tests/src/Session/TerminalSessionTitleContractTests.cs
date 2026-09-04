namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Applies the shared 0.4 payload and failure contract through each public title operation.
/// </summary>
public sealed class TerminalSessionTitleContractTests {
	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public async Task PublicTitleOperationAcceptsMaximumPayload(
		int operation
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string value = new(
			'a',
			OscWriter.MaximumTitlePayloadByteCount
		);

		await InvokeAsync(
			session,
			operation,
			value
		);

		Assert.Equal( 1, output.WriteCount );
		Assert.Equal(
			OscWriter.MaximumTitlePayloadByteCount + 6,
			output.Bytes.Count
		);
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public async Task PublicTitleOperationRejectsOversizedPayloadWithoutWriting(
		int operation
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );
		string value = new(
			'a',
			OscWriter.MaximumTitlePayloadByteCount + 1
		);

		await Assert.ThrowsAsync<ArgumentException>(
			() => InvokeAsync(
				session,
				operation,
				value
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public async Task PublicTitleOperationRejectsControlInjectionWithoutWriting(
		int operation
	) {
		RecordingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		await Assert.ThrowsAsync<ArgumentException>(
			() => InvokeAsync(
				session,
				operation,
				"before\u001b]2;injected\u001b\\after"
			).AsTask()
		);

		Assert.Equal( 0, output.WriteCount );
		Assert.Empty( output.Bytes );
	}

	[Theory]
	[InlineData( 0 )]
	[InlineData( 1 )]
	[InlineData( 2 )]
	public async Task PublicTitleOperationPropagatesOutputFailure(
		int operation
	) {
		FailingTerminalOutput output = new();
		await using TerminalSession session = await OpenSessionAsync( output );

		IOException exception = await Assert.ThrowsAsync<IOException>(
			() => InvokeAsync(
				session,
				operation,
				"title"
			).AsTask()
		);

		Assert.Equal( "write failed", exception.Message );
		Assert.Equal( 1, output.WriteCount );
	}

	private static ValueTask InvokeAsync(
		TerminalSession session,
		int operation,
		string value
	) {
		ArgumentNullException.ThrowIfNull( session );
		ArgumentNullException.ThrowIfNull( value );

		return operation switch {
			0 => session.SetTitleAsync( value ),
			1 => session.SetIconNameAsync( value ),
			2 => session.SetWindowTitleAsync( value ),
			_ => throw new ArgumentOutOfRangeException(
				nameof( operation )
			)
		};
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		ITerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new TestTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb
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
		internal List<byte> Bytes {
			get;
		} = [];

		internal int WriteCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
			this.Bytes.AddRange( buffer.ToArray() );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private sealed class FailingTerminalOutput : ITerminalOutput {
		internal int WriteCount {
			get;
			private set;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			++this.WriteCount;
			throw new IOException( "write failed" );
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
				"Size is not used by this test provider."
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
