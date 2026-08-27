using System.Text;
using Icod.Terminal;
using Icod.TermInfo;

static void Require(
	bool condition,
	string message
) {
	ArgumentNullException.ThrowIfNull( message );

	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

TerminalDescription terminal = TerminalDatabase.BuiltIn.Load( "xterm" );
var provider = new PackageTerminalControlProvider();
var input = new ScriptedTerminalInput(
	Encoding.UTF8.GetBytes( "x" )
);
var output = new RecordingTerminalOutput();
TerminalSession? session = null;

try {
	session = await TerminalSession.OpenAsync(
		provider,
		TerminalEndpoint.StandardInput,
		TerminalEndpoint.StandardOutput,
		input,
		output,
		new TerminalSessionOptions {
			InputMode = TerminalInputMode.CBreak,
			EchoInput = false,
			ConfigureOutput = false,
			ObserveLifecycleEvents = false,
			TerminalOverride = terminal
		}
	);

	Require(
		"xterm" == session.Terminal.Name,
		"The package consumer did not retain the explicit terminal profile."
	);
	Require(
		session.IsInteractive,
		"The injected package-consumer session should be interactive."
	);

	TerminalControlResult<TerminalSize> sizeResult = session.GetSize();
	Require(
		sizeResult.IsAvailable,
		"The injected package-consumer terminal size is unavailable."
	);
	TerminalSize size = sizeResult.GetRequiredValue();
	Require(
		100 == size.Columns && 40 == size.Rows,
		"The package consumer received unexpected terminal dimensions."
	);

	TerminalEvent terminalEvent = await session.ReadEventAsync(
		TimeSpan.FromSeconds( 1 )
	);
	Require(
		TerminalEventKind.Input == terminalEvent.Kind,
		"The package consumer did not receive the scripted input event."
	);
	TerminalInputEvent? inputEvent = terminalEvent.Input;
	Require(
		inputEvent is not null
			&& TerminalInputEventKind.Text == inputEvent.Kind
			&& inputEvent.Character.HasValue
			&& 'x' == inputEvent.Character.Value.Value,
		"The package consumer decoded the scripted UTF-8 input incorrectly."
	);

	await session.WriteTextAsync( "package-smoke" );
	bool capabilityWritten = await session.WriteCapabilityAsync(
		StringCapability.EnterCursorAddressingMode
	);
	Require(
		capabilityWritten,
		"The transitive Icod.TermInfo profile did not expose full-screen entry."
	);

	string serialized = TerminalModeCodec.Serialize(
		provider.BaselineMode
	);
	bool restored = TerminalModeCodec.TryRestore(
		serialized,
		provider.BaselineMode,
		out TerminalModeSnapshot? restoredMode,
		out string? restoreError
	);
	Require(
		restored,
		restoreError ?? "The package consumer could not restore serialized terminal state."
	);
	Require(
		provider.BaselineMode.ConsoleMode == restoredMode!.ConsoleMode,
		"Serialized terminal state did not round-trip through the package."
	);
} finally {
	if ( session is not null ) {
		await session.DisposeAsync();
	}
}

Require(
	2 == provider.SetModeCallCount,
	"The package consumer expected one input-mode application and one baseline restoration."
);
Require(
	0 < output.FlushCallCount,
	"The package consumer did not flush output during deterministic disposal."
);
Require(
	Encoding.UTF8.GetString( output.ToArray() ).Contains(
		"package-smoke",
		StringComparison.Ordinal
	),
	"The package consumer did not emit application text through TerminalSession."
);

Console.WriteLine( "Icod.Terminal package smoke test passed." );

internal sealed class ScriptedTerminalInput : ITerminalInput {
	private readonly byte[] bytes;
	private int offset;

	internal ScriptedTerminalInput(
		byte[] bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		this.bytes = bytes.ToArray();
	}

	public ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();

		if ( this.offset >= this.bytes.Length ) {
			return ValueTask.FromResult( 0 );
		}

		int count = Math.Min(
			buffer.Length,
			this.bytes.Length - this.offset
		);
		this.bytes.AsMemory(
			this.offset,
			count
		).CopyTo( buffer );
		this.offset += count;
		return ValueTask.FromResult( count );
	}
}

internal sealed class RecordingTerminalOutput : ITerminalOutput {
	private readonly MemoryStream stream = new();

	internal int FlushCallCount {
		get;
		private set;
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.stream.Write( buffer.Span );
		return ValueTask.CompletedTask;
	}

	public ValueTask FlushAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.FlushCallCount++;
		return ValueTask.CompletedTask;
	}

	internal byte[] ToArray() {
		return this.stream.ToArray();
	}
}

internal sealed class PackageTerminalControlProvider : ITerminalControlProvider {
	internal PackageTerminalControlProvider() {
		this.BaselineMode = TerminalModeSnapshot.CreateWindowsConsole(
			TerminalConsoleDirection.Input,
			0x00000007u
		);
	}

	internal TerminalModeSnapshot BaselineMode {
		get;
	}

	internal int SetModeCallCount {
		get;
		private set;
	}

	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );

		TerminalControlCapabilities capabilities =
			TerminalControlCapabilities.Attachment
				| TerminalControlCapabilities.LiveSize;
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

		return TerminalControlResult<TerminalSize>.Available(
			new TerminalSize(
				100,
				40
			)
		);
	}

	public TerminalControlResult<TerminalModeSnapshot> GetMode(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );

		if ( TerminalEndpointKind.FileDescriptor != endpoint.Kind
			|| 0 != endpoint.FileDescriptor ) {
			return TerminalControlResult<TerminalModeSnapshot>.Unavailable(
				"Only the scripted standard-input endpoint has a terminal mode."
			);
		}

		return TerminalControlResult<TerminalModeSnapshot>.Available(
			this.BaselineMode
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
		if ( TerminalEndpointKind.FileDescriptor != endpoint.Kind
			|| 0 != endpoint.FileDescriptor ) {
			return TerminalControlMutationResult.Unavailable(
				"Only the scripted standard-input endpoint can be mutated."
			);
		}
		if ( TerminalPlatformKind.WindowsConsole != mode.Platform
			|| TerminalConsoleDirection.Input != mode.ConsoleDirection ) {
			return TerminalControlMutationResult.Failed(
				"The scripted provider expected a Windows console input mode."
			);
		}

		this.SetModeCallCount++;
		return TerminalControlMutationResult.Success();
	}
}
