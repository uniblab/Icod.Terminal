using System.Text;
using System.Threading.Channels;
using Icod.DCurses;
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

static byte ToByte(
	ushort value
) {
	return (byte)( value >> 8 );
}

ScriptedTransport transport = new();
RecordingTerminalControlProvider provider = new();
TerminalDescription terminal = new TerminalDescriptionBuilder(
	"dcurses-color-observation-acceptance"
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
	.SetExtendedString(
		"setrgbf",
		"<rgbf:%p1%d,%p2%d,%p3%d>"
	)
	.SetExtendedString(
		"setrgbb",
		"<rgbb:%p1%d,%p2%d,%p3%d>"
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
		ObserveLifecycleEvents = false,
		InputDecoderOptions = new TerminalInputDecoderOptions {
			EscapeSequenceTimeout = TimeSpan.Zero
		}
	}
);

Task<TerminalColor> foregroundQuery = terminalSession.QueryPaletteColorAsync(
	2,
	TimeSpan.FromSeconds( 30 )
).AsTask();
await transport.WaitForWriteCountAsync( 1 );
Require(
	transport.GetWrite( 0 ).AsSpan().SequenceEqual(
		Encoding.ASCII.GetBytes( "\u001b]4;2;?\u001b\\" )
	),
	"The expected OSC 4 palette observation query was not emitted."
);
transport.Publish(
	Encoding.ASCII.GetBytes( "\u001b]4;2;rgb:1234/5678/9abc\u001b\\" )
);
TerminalColor observedForeground = await foregroundQuery;

Task<TerminalColor> backgroundQuery = terminalSession.QueryDynamicColorAsync(
	TerminalDynamicColor.DefaultBackground,
	TimeSpan.FromSeconds( 30 )
).AsTask();
await transport.WaitForWriteCountAsync( 2 );
Require(
	transport.GetWrite( 1 ).AsSpan().SequenceEqual(
		Encoding.ASCII.GetBytes( "\u001b]11;?\u001b\\" )
	),
	"The expected OSC 11 dynamic-color observation query was not emitted."
);
transport.Publish(
	Encoding.ASCII.GetBytes( "\u001b]11;rgb:fedc/ba98/7654\u001b\\" )
);
TerminalColor observedBackground = await backgroundQuery;

Require(
	new TerminalColor( 0x1234, 0x5678, 0x9abc ) == observedForeground,
	"The typed foreground observation did not preserve the expected 16-bit channels."
);
Require(
	new TerminalColor( 0xfedc, 0xba98, 0x7654 ) == observedBackground,
	"The typed background observation did not preserve the expected 16-bit channels."
);

CursesColor foreground = CursesColor.Rgb(
	ToByte( observedForeground.Red ),
	ToByte( observedForeground.Green ),
	ToByte( observedForeground.Blue )
);
CursesColor background = CursesColor.Rgb(
	ToByte( observedBackground.Red ),
	ToByte( observedBackground.Green ),
	ToByte( observedBackground.Blue )
);
CursesStyle observedStyle = new(
	foreground,
	background
);

await using CursesSession curses = await CursesSession.OpenAsync(
	terminalSession,
	new CursesSessionOptions {
		UseAlternateScreen = false,
		EnableKeypad = false,
		HideCursor = false
	}
);
curses.StandardScreen.Write(
	"observed",
	observedStyle
);
await curses.RefreshAsync();

Require(
	transport.ContainsWrite( Encoding.Latin1.GetBytes( "<rgbf:18,86,154>" ) ),
	"Icod.DCurses did not consume the observed foreground through its RGB style path."
);
Require(
	transport.ContainsWrite( Encoding.Latin1.GetBytes( "<rgbb:254,186,118>" ) ),
	"Icod.DCurses did not consume the observed background through its RGB style path."
);
Require(
	transport.ContainsWrite( Encoding.UTF8.GetBytes( "observed" ) ),
	"Icod.DCurses did not render the styled downstream payload."
);

Console.WriteLine(
	"Icod.DCurses typed terminal-color observation acceptance passed."
);

internal sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
	private readonly object sync = new();
	private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
	private readonly SemaphoreSlim writeSignal = new( 0 );
	private readonly List<byte[]> writes = [];
	private byte[]? pending;
	private int pendingOffset;

	internal byte[] GetWrite(
		int index
	) {
		lock ( this.sync ) {
			return this.writes[ index ].ToArray();
		}
	}

	internal bool ContainsWrite(
		byte[] expected
	) {
		ArgumentNullException.ThrowIfNull( expected );
		lock ( this.sync ) {
			return this.writes.Any(
				write => write.AsSpan().SequenceEqual( expected )
			);
		}
	}

	internal void Publish(
		byte[] bytes
	) {
		ArgumentNullException.ThrowIfNull( bytes );
		if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
			throw new InvalidOperationException( "The scripted input channel is closed." );
		}
	}

	internal async ValueTask WaitForWriteCountAsync(
		int expected
	) {
		using CancellationTokenSource timeout = new(
			TimeSpan.FromSeconds( 5 )
		);
		while ( true ) {
			lock ( this.sync ) {
				if ( expected <= this.writes.Count ) {
					return;
				}
			}
			await this.writeSignal.WaitAsync(
				timeout.Token
			).ConfigureAwait( false );
		}
	}

	public async ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		if ( this.pending is null ) {
			this.pending = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			this.pendingOffset = 0;
		}

		int count = Math.Min(
			buffer.Length,
			this.pending.Length - this.pendingOffset
		);
		this.pending.AsSpan( this.pendingOffset, count ).CopyTo( buffer.Span );
		this.pendingOffset += count;
		if ( this.pendingOffset == this.pending.Length ) {
			this.pending = null;
			this.pendingOffset = 0;
		}
		return count;
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		lock ( this.sync ) {
			this.writes.Add( buffer.ToArray() );
		}
		this.writeSignal.Release();
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
