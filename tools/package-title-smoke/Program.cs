using Icod.Terminal;

static void Require(
	bool condition,
	string message
) {
	ArgumentException.ThrowIfNullOrWhiteSpace( message );

	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

RecordingTerminalOutput output = new();
await using TerminalSession session = await TerminalSession.OpenAsync(
	new PackageTerminalControlProvider(),
	TerminalEndpoint.StandardInput,
	TerminalEndpoint.StandardOutput,
	new EmptyTerminalInput(),
	output,
	new TerminalSessionOptions {
		ConfigureOutput = false,
		ObserveLifecycleEvents = false,
		TerminalOverride = Icod.TermInfo.TerminalProfiles.Dumb
	}
);

await session.SetTitleAsync( "both" );
await session.SetIconNameAsync( "icon" );
await session.SetWindowTitleAsync( "window" );

byte[] expected = Convert.FromHexString(
	"1B5D303B626F74681B5C"
		+ "1B5D313B69636F6E1B5C"
		+ "1B5D323B77696E646F771B5C"
);
Require(
	expected.SequenceEqual( output.Bytes ),
	"The package-only OSC 0/1/2 title smoke emitted unexpected bytes."
);
Require(
	0 == output.FlushCount,
	"OSC title operations must not flush implicitly."
);

Console.WriteLine( "Icod.Terminal package title smoke test passed." );

internal sealed class EmptyTerminalInput : ITerminalInput {
	public ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult( 0 );
	}
}

internal sealed class RecordingTerminalOutput : ITerminalOutput {
	internal List<byte> Bytes {
		get;
	} = [];

	internal int FlushCount {
		get;
		private set;
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.Bytes.AddRange( buffer.ToArray() );
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

internal sealed class PackageTerminalControlProvider : ITerminalControlProvider {
	private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
		TerminalConsoleDirection.Input,
		0x00000007u
	);

	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );

		TerminalControlCapabilities capabilities = TerminalControlCapabilities.Attachment;
		if ( TerminalEndpointKind.FileDescriptor == endpoint.Kind
			&& 0 == endpoint.FileDescriptor ) {
			capabilities |= TerminalControlCapabilities.ModeRead
				| TerminalControlCapabilities.ModeWrite;
		}

		return TerminalControlResult<TerminalEndpointObservation>.Available(
			new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.WindowsConsole,
				capabilities
			)
		);
	}

	public TerminalControlResult<TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalSize>.Unsupported(
			"Size is not required by the package title smoke."
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
