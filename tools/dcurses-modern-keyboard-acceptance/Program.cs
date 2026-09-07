using System.Text;
using System.Threading.Channels;
using Icod.DCurses;
using Icod.Terminal;
using Icod.TermInfo;

const string ProbeRequest = "\u001b[?u\u001b[c";
const string PushAllKeys = "\u001b[>31u";
const string PopKeyboard = "\u001b[<u";
const string EnterAlternateScreen = "<A+>";
const string ExitAlternateScreen = "<A->";

static void Require(
	bool condition,
	string message
) {
	ArgumentNullException.ThrowIfNull( message );
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

DuplexTerminalTransport transport = new();
RecordingTerminalControlProvider provider = new();
TerminalDescription terminal = new TerminalDescriptionBuilder(
	"dcurses-modern-keyboard-acceptance"
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
		StringCapability.CursorAddress,
		"<cup:%p1%d,%p2%d>"
	)
	.SetString(
		StringCapability.ExitAttributeMode,
		"<sgr0>"
	)
	.SetString(
		StringCapability.OriginalColorPair,
		"<op>"
	)
	.SetString( StringCapability.KeyMouse, "\u001b[<" )
	.SetExtendedString( "BE", "<P+>" )
	.SetExtendedString( "BD", "<P->" )
	.SetExtendedString( "PS", "\u001b[200~" )
	.SetExtendedString( "PE", "\u001b[201~" )
	.SetExtendedString( "fe", "<F+>" )
	.SetExtendedString( "fd", "<F->" )
	.SetExtendedString( "kxIN", "\u001b[I" )
	.SetExtendedString( "kxOUT", "\u001b[O" )
	.SetExtendedString(
		"XM",
		"\u001b[?1006;1000%?%p1%{1}%=%th%el%;"
	)
	.SetExtendedString(
		"xm",
		"\u001b[<%i%p3%d;%p1%d;%p2%d;%?%p4%tM%em%;"
	)
	.Build();

await using TerminalSession terminalSession = await TerminalSession.OpenAsync(
	provider,
	TerminalEndpoint.StandardInput,
	TerminalEndpoint.StandardOutput,
	transport,
	transport,
	new TerminalSessionOptions {
		TerminalOverride = terminal,
		ConfigureOutput = false,
		ObserveLifecycleEvents = false
	}
);

TerminalInputProtocolLease protocols = (
	await terminalSession.AcquireInputProtocolsAsync(
		new TerminalInputProtocolOptions {
			BracketedPaste = true,
			FocusReporting = true,
			MouseTrackingMode = TerminalMouseTrackingMode.ButtonEvents,
			KeyboardReportingMode = TerminalKeyboardReportingMode.AllKeys
		}
	)
).GetRequiredValue();

Require(
	transport.ContainsWrite( ProbeRequest ),
	"Kitty support was not negotiated before rich-input acquisition."
);
Require(
	transport.ContainsWrite( PushAllKeys ),
	"Kitty AllKeys reporting was not pushed before DCurses opened."
);
Require(
	transport.ContainsWrite( "<P+>" )
		&& transport.ContainsWrite( "<F+>" )
		&& transport.ContainsWrite( "\u001b[?1006h" )
		&& transport.ContainsWrite( "\u001b[?1000h" ),
	"Existing rich-input protocols were not composed with the Kitty lease."
);

transport.ClearWrites();
CursesSession curses = await CursesSession.OpenAsync(
	terminalSession,
	new CursesSessionOptions {
		UseAlternateScreen = true,
		EnableKeypad = false,
		HideCursor = false
	}
);

try {
	RequireOrdered(
		transport,
		PopKeyboard,
		EnterAlternateScreen,
		PushAllKeys,
		"DCurses full-screen entry did not move Kitty ownership to the alternate screen."
	);

	int beforeRefresh = transport.WriteCount;
	curses.StandardScreen.Write( "K" );
	await curses.RefreshAsync();
	Require(
		beforeRefresh < transport.WriteCount,
		"DCurses RefreshAsync produced no terminal output while Kitty reporting was active."
	);

	transport.QueueInput(
		Encoding.UTF8.GetBytes(
			"\u001b[97:65:113;6:2;120u"
				+ "\u001b[I"
				+ "\u001b[200~ok\u001b[201~"
		)
	);

	TerminalInputEvent key = RequireInput(
		await terminalSession.ReadEventAsync()
	);
	Require(
		TerminalInputEventKind.Key == key.Kind
			&& TerminalKey.Character == key.Key
			&& key.Character.HasValue
			&& 'a' == key.Character.Value.Value
			&& key.ShiftedCharacter.HasValue
			&& 'A' == key.ShiftedCharacter.Value.Value
			&& key.BaseLayoutCharacter.HasValue
			&& 'q' == key.BaseLayoutCharacter.Value.Value
			&& TerminalKeyEventPhase.Repeat == key.KeyPhase
			&& ( TerminalKeyModifiers.Shift | TerminalKeyModifiers.Control ) == key.Modifiers
			&& string.Equals( "x", key.AssociatedText, StringComparison.Ordinal ),
		"The negotiated Kitty event did not survive the real DCurses full-screen session."
	);

	TerminalInputEvent focus = RequireInput(
		await terminalSession.ReadEventAsync()
	);
	Require(
		TerminalInputEventKind.Focus == focus.Kind
			&& TerminalFocusState.Focused == focus.Focus?.State,
		"Focus reporting did not coexist with negotiated Kitty input under DCurses."
	);

	TerminalInputEvent pasteBegin = RequireInput(
		await terminalSession.ReadEventAsync()
	);
	TerminalInputEvent pasteData = RequireInput(
		await terminalSession.ReadEventAsync()
	);
	TerminalInputEvent pasteEnd = RequireInput(
		await terminalSession.ReadEventAsync()
	);
	Require(
		TerminalPastePhase.Begin == pasteBegin.Paste?.Phase
			&& TerminalPastePhase.Data == pasteData.Paste?.Phase
			&& string.Equals( "ok", pasteData.Paste?.Text, StringComparison.Ordinal )
			&& TerminalPastePhase.End == pasteEnd.Paste?.Phase,
		"Bracketed paste did not coexist with negotiated Kitty input under DCurses."
	);

	transport.ClearWrites();
} finally {
	await curses.DisposeAsync();
}

RequireOrdered(
	transport,
	PopKeyboard,
	ExitAlternateScreen,
	PushAllKeys,
	"DCurses full-screen exit did not return Kitty ownership to the main screen."
);

transport.ClearWrites();
await protocols.DisposeAsync();
Require(
	transport.ContainsWrite( PopKeyboard )
		&& transport.ContainsWrite( "<P->" )
		&& transport.ContainsWrite( "<F->" )
		&& transport.ContainsWrite( "\u001b[?1000l" )
		&& transport.ContainsWrite( "\u001b[?1006l" ),
	"Final rich-input release did not restore the composed protocol baseline."
);

Console.WriteLine(
	"Icod.DCurses modern-keyboard/full-screen rich-input acceptance passed."
);

static TerminalInputEvent RequireInput(
	TerminalEvent terminalEvent
) {
	ArgumentNullException.ThrowIfNull( terminalEvent );
	if ( TerminalEventKind.Input != terminalEvent.Kind || terminalEvent.Input is null ) {
		throw new InvalidOperationException(
			"Expected a terminal input event from the acceptance transport."
		);
	}
	return terminalEvent.Input;
}

static void RequireOrdered(
	DuplexTerminalTransport transport,
	string first,
	string second,
	string third,
	string message
) {
	ArgumentNullException.ThrowIfNull( transport );
	ArgumentNullException.ThrowIfNull( first );
	ArgumentNullException.ThrowIfNull( second );
	ArgumentNullException.ThrowIfNull( third );
	ArgumentNullException.ThrowIfNull( message );

	int firstIndex = transport.IndexOf( first );
	int secondIndex = transport.IndexOf( second );
	int thirdIndex = transport.IndexOf( third );
	Require(
		0 <= firstIndex && firstIndex < secondIndex && secondIndex < thirdIndex,
		message
	);
}

internal sealed class DuplexTerminalTransport : ITerminalInput, ITerminalOutput {
	private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
	private readonly object sync = new();
	private readonly List<string> writes = [];

	internal int WriteCount {
		get {
			lock ( this.sync ) {
				return this.writes.Count;
			}
		}
	}

	internal bool ContainsWrite(
		string expected
	) {
		ArgumentNullException.ThrowIfNull( expected );
		return 0 <= this.IndexOf( expected );
	}

	internal int IndexOf(
		string expected
	) {
		ArgumentNullException.ThrowIfNull( expected );
		lock ( this.sync ) {
			return this.writes.FindIndex(
				value => string.Equals(
					value,
					expected,
					StringComparison.Ordinal
				)
			);
		}
	}

	internal void ClearWrites() {
		lock ( this.sync ) {
			this.writes.Clear();
		}
	}

	internal void QueueInput(
		byte[] value
	) {
		ArgumentNullException.ThrowIfNull( value );
		if ( !this.input.Writer.TryWrite( value.ToArray() ) ) {
			throw new InvalidOperationException(
				"The acceptance input could not be queued."
			);
		}
	}

	public async ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		byte[] value = await this.input.Reader.ReadAsync(
			cancellationToken
		).ConfigureAwait( false );
		if ( value.Length > buffer.Length ) {
			throw new InvalidOperationException(
				"The acceptance input exceeds the terminal read buffer."
			);
		}
		value.AsSpan().CopyTo( buffer.Span );
		return value.Length;
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		string value = Encoding.Latin1.GetString( buffer.Span );
		lock ( this.sync ) {
			this.writes.Add( value );
		}

		if ( string.Equals( ProbeRequest, value, StringComparison.Ordinal ) ) {
			this.QueueInput(
				Encoding.ASCII.GetBytes( "\u001b[?0u\u001b[?1;2c" )
			);
		}
		return ValueTask.CompletedTask;
	}

	public ValueTask FlushAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}
}

internal sealed class RecordingTerminalControlProvider : ITerminalControlProvider {
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
					| TerminalControlCapabilities.LiveSize
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
			)
		);
	}

	public TerminalControlResult<TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalSize>.Available(
			new TerminalSize( 80, 24 )
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
