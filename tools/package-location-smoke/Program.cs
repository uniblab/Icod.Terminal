using Icod.Terminal;
using Icod.TermInfo;

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
		TerminalOverride = TerminalProfiles.Dumb
	}
);

await session.PublishCurrentLocationAsync(
	"/srv/My Project/猫/%20",
	TerminalLocationPathStyle.Posix,
	"example.com"
);
await session.PublishCurrentLocationAsync(
	"c:\\Development\\Icod",
	TerminalLocationPathStyle.WindowsDrive
);
await session.PublishCurrentLocationAsync(
	"\\\\server\\share\\dir",
	TerminalLocationPathStyle.WindowsUnc
);

byte[] expected = Convert.FromHexString(
	"1B5D373B66696C653A2F2F6578616D706C652E636F6D2F7372762F4D7925323050726F6A6563742F2545372538432541422F25323532301B5C"
		+ "1B5D373B66696C653A2F2F2F433A2F446576656C6F706D656E742F49636F641B5C"
		+ "1B5D373B66696C653A2F2F7365727665722F73686172652F6469721B5C"
);
Require(
	expected.SequenceEqual( output.Bytes ),
	"The package-only OSC 7 location smoke emitted unexpected bytes."
);
Require(
	0 == output.FlushCount,
	"OSC 7 current-location publication must not flush implicitly."
);

Console.WriteLine( "Icod.Terminal package location smoke test passed." );

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
			"Size is not required by the package location smoke."
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
