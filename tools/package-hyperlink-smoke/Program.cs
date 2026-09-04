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

await session.WriteHyperlinkAsync(
	"linked",
	"https://example.com/a%2fb?q=1#part",
	"package-1"
);

await using ( TerminalHyperlinkLease outer = await session.AcquireHyperlinkAsync(
	"https://example.com/outer",
	"outer"
) ) {
	await session.WriteTextAsync( "A" );
	await using ( TerminalHyperlinkLease inner = await session.AcquireHyperlinkAsync(
		"https://example.com/inner",
		"inner"
	) ) {
		await session.WriteTextAsync( "B" );
	}
	await session.WriteTextAsync( "C" );
}

Require(
	"https://example.com/outer" == outer.Uri,
	"The package-only OSC 8 lease exposed an unexpected canonical URI."
);
Require(
	"outer" == outer.Identifier,
	"The package-only OSC 8 lease exposed an unexpected identifier."
);

byte[] expected = Convert.FromHexString(
	"1B5D383B69643D7061636B6167652D313B68747470733A2F2F6578616D706C652E636F6D2F61253246623F713D3123706172741B5C"
		+ "6C696E6B6564"
		+ "1B5D383B3B1B5C"
		+ "1B5D383B69643D6F757465723B68747470733A2F2F6578616D706C652E636F6D2F6F757465721B5C"
		+ "41"
		+ "1B5D383B69643D696E6E65723B68747470733A2F2F6578616D706C652E636F6D2F696E6E65721B5C"
		+ "42"
		+ "1B5D383B69643D6F757465723B68747470733A2F2F6578616D706C652E636F6D2F6F757465721B5C"
		+ "43"
		+ "1B5D383B3B1B5C"
);
Require(
	expected.SequenceEqual( output.Bytes ),
	"The package-only OSC 8 consumer emitted unexpected bytes."
);
Require(
	0 == output.FlushCount,
	"OSC 8 hyperlink operations must not flush implicitly."
);

Console.WriteLine( "Icod.Terminal package hyperlink smoke test passed." );

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
			"Size is not required by the package hyperlink smoke."
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
