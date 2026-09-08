/*
	Icod.Terminal.DCursesColorObservationAcceptance
	Downstream Icod.DCurses acceptance utility for Icod.Terminal integration contracts.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU General Public License for more details.

	You should have received a copy of the GNU General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
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

TerminalSession terminalSession = await TerminalSession.OpenAsync(
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

TerminalColor foregroundOwned = new( 0xabcd, 0x4567, 0x89ef );
Task<TerminalPaletteColorLease> foregroundAcquisition = terminalSession.AcquirePaletteColorAsync(
	2,
	foregroundOwned,
	TimeSpan.FromSeconds( 30 )
).AsTask();
await transport.WaitForWriteCountAsync( 1 );
Require(
	transport.GetWrite( 0 ).AsSpan().SequenceEqual(
		Encoding.ASCII.GetBytes( "\u001b]4;2;?\u001b\\" )
	),
	"Scoped foreground ownership did not query the external OSC 4 baseline before mutation."
);
transport.Publish(
	Encoding.ASCII.GetBytes( "\u001b]4;2;rgb:1234/5678/9abc\u001b\\" )
);
TerminalPaletteColorLease foregroundLease = await foregroundAcquisition;
await transport.WaitForWriteCountAsync( 2 );
Require(
	transport.GetWrite( 1 ).AsSpan().SequenceEqual(
		Encoding.ASCII.GetBytes( "\u001b]4;2;rgb:abcd/4567/89ef\u001b\\" )
	),
	"Scoped foreground ownership did not apply the requested OSC 4 color."
);

TerminalColor backgroundOwned = new( 0x2468, 0xace0, 0x1357 );
Task<TerminalDynamicColorLease> backgroundAcquisition = terminalSession.AcquireDynamicColorAsync(
	TerminalDynamicColor.DefaultBackground,
	backgroundOwned,
	TimeSpan.FromSeconds( 30 )
).AsTask();
await transport.WaitForWriteCountAsync( 3 );
Require(
	transport.GetWrite( 2 ).AsSpan().SequenceEqual(
		Encoding.ASCII.GetBytes( "\u001b]11;?\u001b\\" )
	),
	"Scoped background ownership did not query the external OSC 11 baseline before mutation."
);
transport.Publish(
	Encoding.ASCII.GetBytes( "\u001b]11;rgb:fedc/ba98/7654\u001b\\" )
);
TerminalDynamicColorLease backgroundLease = await backgroundAcquisition;
await transport.WaitForWriteCountAsync( 4 );
Require(
	transport.GetWrite( 3 ).AsSpan().SequenceEqual(
		Encoding.ASCII.GetBytes( "\u001b]11;rgb:2468/ace0/1357\u001b\\" )
	),
	"Scoped background ownership did not apply the requested OSC 11 color."
);

Require(
	2 == foregroundLease.Index,
	"The scoped foreground lease did not preserve its palette identity."
);
Require(
	foregroundOwned == foregroundLease.Color,
	"The scoped foreground lease did not preserve its requested 16-bit color."
);
Require(
	TerminalDynamicColor.DefaultBackground == backgroundLease.Kind,
	"The scoped background lease did not preserve its dynamic-color identity."
);
Require(
	backgroundOwned == backgroundLease.Color,
	"The scoped background lease did not preserve its requested 16-bit color."
);

CursesColor foreground = CursesColor.Rgb(
	ToByte( foregroundLease.Color.Red ),
	ToByte( foregroundLease.Color.Green ),
	ToByte( foregroundLease.Color.Blue )
);
CursesColor background = CursesColor.Rgb(
	ToByte( backgroundLease.Color.Red ),
	ToByte( backgroundLease.Color.Green ),
	ToByte( backgroundLease.Color.Blue )
);
CursesStyle ownedStyle = new(
	foreground,
	background
);

CursesSession curses = await CursesSession.OpenAsync(
	terminalSession,
	new CursesSessionOptions {
		UseAlternateScreen = false,
		EnableKeypad = false,
		HideCursor = false
	}
);

curses.StandardScreen.Write(
	"owned",
	ownedStyle
);
await curses.RefreshAsync();

Require(
	transport.ContainsWrite( Encoding.Latin1.GetBytes( "<rgbf:171,69,137>" ) ),
	"Icod.DCurses did not consume the scoped foreground through its RGB style path."
);
Require(
	transport.ContainsWrite( Encoding.Latin1.GetBytes( "<rgbb:36,172,19>" ) ),
	"Icod.DCurses did not consume the scoped background through its RGB style path."
);
Require(
	transport.ContainsWrite( Encoding.UTF8.GetBytes( "owned" ) ),
	"Icod.DCurses did not render the scoped-color downstream payload."
);

int writesBeforeCursesDispose = transport.WriteCount;
await curses.DisposeAsync();

Require(
	transport.ContainsWriteSince(
		writesBeforeCursesDispose,
		Encoding.ASCII.GetBytes( "\u001b]11;rgb:fedc/ba98/7654\u001b\\" )
	),
	"Curses-owned TerminalSession disposal did not replay the exact observed OSC 11 baseline."
);
Require(
	transport.ContainsWriteSince(
		writesBeforeCursesDispose,
		Encoding.ASCII.GetBytes( "\u001b]4;2;rgb:1234/5678/9abc\u001b\\" )
	),
	"Curses-owned TerminalSession disposal did not replay the exact observed OSC 4 baseline."
);
Require(
	!transport.ContainsWrite( Encoding.ASCII.GetBytes( "\u001b]111\u001b\\" ) ),
	"Scoped dynamic-color restoration incorrectly used OSC 111 reset."
);
Require(
	!transport.ContainsWrite( Encoding.ASCII.GetBytes( "\u001b]104;2\u001b\\" ) ),
	"Scoped palette restoration incorrectly used OSC 104 reset."
);

int writesAfterCursesDispose = transport.WriteCount;
await backgroundLease.DisposeAsync();
await foregroundLease.DisposeAsync();
Require(
	writesAfterCursesDispose == transport.WriteCount,
	"Late scoped-color lease disposal emitted output after CursesSession had disposed its owned TerminalSession."
);

await terminalSession.DisposeAsync();
Require(
	writesAfterCursesDispose == transport.WriteCount,
	"Repeated TerminalSession disposal emitted output after CursesSession ownership cleanup completed."
);

Console.WriteLine(
	"Icod.DCurses lifecycle-safe scoped terminal-color acceptance passed."
);

internal sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
	private readonly object sync = new();
	private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
	private readonly SemaphoreSlim writeSignal = new( 0 );
	private readonly List<byte[]> writes = [];
	private byte[]? pending;
	private int pendingOffset;

	internal int WriteCount {
		get {
			lock ( this.sync ) {
				return this.writes.Count;
			}
		}
	}

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

	internal bool ContainsWriteSince(
		int startIndex,
		byte[] expected
	) {
		if ( 0 > startIndex ) {
			throw new ArgumentOutOfRangeException( nameof( startIndex ) );
		}
		ArgumentNullException.ThrowIfNull( expected );

		lock ( this.sync ) {
			return this.writes
				.Skip( startIndex )
				.Any( write => write.AsSpan().SequenceEqual( expected ) );
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
