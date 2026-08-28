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

static async ValueTask<TerminalInputEvent> ReadInputAsync(
	TerminalSession session,
	string description
) {
	ArgumentNullException.ThrowIfNull( session );
	ArgumentException.ThrowIfNullOrWhiteSpace( description );

	TerminalEvent terminalEvent = await session.ReadEventAsync(
		TimeSpan.FromSeconds( 1 )
	);
	Require(
		TerminalEventKind.Input == terminalEvent.Kind
			&& terminalEvent.Input is not null,
		$"The package consumer did not receive {description} as an input event."
	);
	return terminalEvent.Input!;
}

TerminalDescription terminal = TerminalDatabase.BuiltIn.Load( "xterm" );
var provider = new PackageTerminalControlProvider();
string scriptedText =
	"x"
		+ "\u001b[I"
		+ "\u001b[200~hello\u001b[201~"
		+ "\u001b[<0;3;4M"
		+ "\u001b[1;5A"
;
var input = new ScriptedTerminalInput(
	Encoding.UTF8.GetBytes( scriptedText )
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
			TerminalOverride = terminal,
			InputDecoderOptions = new TerminalInputDecoderOptions {
				PasteChunkBytes = 3
			}
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

	TerminalControlResult<TerminalInputProtocolLease> protocolResult =
		await session.AcquireInputProtocolsAsync(
			new TerminalInputProtocolOptions {
				BracketedPaste = true,
				FocusReporting = true,
				MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents
			}
		);
	Require(
		protocolResult.IsAvailable,
		protocolResult.Message
			?? "The xterm package profile did not expose the required rich-input protocols."
	);
	TerminalInputProtocolLease protocolLease = protocolResult.GetRequiredValue();
	Require(
		protocolLease.BracketedPaste
			&& protocolLease.FocusReporting
			&& TerminalMouseTrackingMode.ButtonEvents == protocolLease.MouseTrackingMode,
		"The package consumer received an unexpected rich-input protocol lease."
	);

	TerminalInputEvent text = await ReadInputAsync(
		session,
		"ordinary UTF-8 text"
	);
	Require(
		TerminalInputEventKind.Text == text.Kind
			&& text.Character.HasValue
			&& 'x' == text.Character.Value.Value,
		"The package consumer decoded ordinary UTF-8 input incorrectly."
	);

	TerminalInputEvent focus = await ReadInputAsync(
		session,
		"a focus report"
	);
	TerminalFocusEvent focusPayload = focus.Focus
		?? throw new InvalidOperationException(
			"The package consumer focus event did not carry a focus payload."
		);
	Require(
		TerminalInputEventKind.Focus == focus.Kind
			&& TerminalFocusState.Focused == focusPayload.State,
		"The package consumer decoded focus input incorrectly."
	);

	TerminalInputEvent pasteBegin = await ReadInputAsync(
		session,
		"a paste-begin frame"
	);
	TerminalInputEvent pasteDataOne = await ReadInputAsync(
		session,
		"the first paste data chunk"
	);
	TerminalInputEvent pasteDataTwo = await ReadInputAsync(
		session,
		"the second paste data chunk"
	);
	TerminalInputEvent pasteEnd = await ReadInputAsync(
		session,
		"a paste-end frame"
	);
	TerminalPasteEvent pasteBeginPayload = pasteBegin.Paste
		?? throw new InvalidOperationException(
			"The package consumer paste-begin event did not carry a paste payload."
		);
	TerminalPasteEvent pasteDataOnePayload = pasteDataOne.Paste
		?? throw new InvalidOperationException(
			"The package consumer first paste-data event did not carry a paste payload."
		);
	TerminalPasteEvent pasteDataTwoPayload = pasteDataTwo.Paste
		?? throw new InvalidOperationException(
			"The package consumer second paste-data event did not carry a paste payload."
		);
	TerminalPasteEvent pasteEndPayload = pasteEnd.Paste
		?? throw new InvalidOperationException(
			"The package consumer paste-end event did not carry a paste payload."
		);
	Require(
		TerminalPastePhase.Begin == pasteBeginPayload.Phase
			&& TerminalPastePhase.Data == pasteDataOnePayload.Phase
			&& "hel" == pasteDataOnePayload.Text
			&& TerminalPastePhase.Data == pasteDataTwoPayload.Phase
			&& "lo" == pasteDataTwoPayload.Text
			&& TerminalPastePhase.End == pasteEndPayload.Phase,
		"The package consumer did not preserve bounded bracketed-paste framing."
	);

	TerminalInputEvent mouse = await ReadInputAsync(
		session,
		"an SGR mouse report"
	);
	TerminalMouseEvent mousePayload = mouse.Mouse
		?? throw new InvalidOperationException(
			"The package consumer mouse event did not carry a mouse payload."
		);
	Require(
		TerminalInputEventKind.Mouse == mouse.Kind
			&& TerminalMouseAction.Press == mousePayload.Action
			&& TerminalMouseButton.Primary == mousePayload.Button
			&& 2 == mousePayload.Column
			&& 3 == mousePayload.Row,
		"The package consumer did not normalize SGR mouse input correctly."
	);

	TerminalInputEvent modifiedKey = await ReadInputAsync(
		session,
		"a traditional modified key"
	);
	Require(
		TerminalInputEventKind.Key == modifiedKey.Kind
			&& TerminalKey.Up == modifiedKey.Key
			&& TerminalKeyModifiers.Control == modifiedKey.Modifiers,
		"The package consumer did not normalize Control+Up correctly."
	);

	await protocolLease.DisposeAsync();

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
