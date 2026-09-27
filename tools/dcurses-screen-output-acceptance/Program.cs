/*
	Icod.Terminal.DCursesScreenOutputAcceptance
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
using Icod.DCurses;
using Icod.Terminal;
using Icod.TermInfo;
using Icod.Terminal.ScreenOutput.Sample;

// Fixture-only TermInfo access: the controlled renderer source stays Terminal-only.
foreach ( bool recoverable in new[] { false, true } ) {
	RecordingOutput output = new();
	TestControlProvider provider = new();
	TerminalDescriptionBuilder builder = new( "screen-output-witness" );
	builder.SetString( StringCapability.CursorAddress, "<cup:%p1%d,%p2%d>" );
	builder.SetString( StringCapability.EnterBoldMode, "<bold>" );
	if ( recoverable ) {
		builder.SetString( StringCapability.ExitAttributeMode, "<reset>" );
	}
	await using TerminalSession session = await TerminalSession.OpenAsync(
		provider, TerminalEndpoint.StandardInput,
		TerminalEndpoint.StandardOutput, new EmptyInput(), output,
		new TerminalSessionOptions {
			TerminalOverride = builder.Build(), ConfigureOutput = false,
			ObserveLifecycleEvents = false
		}
	);
	if ( !recoverable ) {
		Require( !await ScreenOutputExample.DrawFrameAsync( session, "sample" )
			&& output.Text.Length == 0, "Sample must reject an unavailable baseline without output." );
		bool rejected = false;
		try {
			await FutureDcursesRenderer.RefreshAsync( session );
		} catch ( InvalidOperationException ) {
			rejected = true;
		}
		Require( rejected && output.Text.Length == 0,
			"Missing rendition baseline must reject refresh before any output." );
		continue;
	}
	await FutureDcursesRenderer.RefreshAsync( session );
	const string expected = "\u001b[?2026h<cup:0,0><reset><bold>─"
		+ "\u001b]8;;https://github.com/uniblab/Icod.Terminal\u001b\\screen contracts\u001b]8;;\u001b\\"
		+ "\u001b[?2026l";
	Require( output.Text == expected && output.FlushCount == 1,
		"Refresh must execute with exact plan/text/hyperlink/framing order: " + output.Text );
	TerminalScreenOutputTransaction stale = session.CreateScreenOutputTransaction();
	stale.WriteText( "stale" );
	await session.WriteTextAsync( "intervening" );
	output.Clear();
	bool staleRejected = false;
	try {
		await stale.CommitAsync();
	} catch ( InvalidOperationException ) {
		staleRejected = true;
	}
	Require( staleRejected && output.Text.Length == 0, "Stale refresh must emit nothing." );
	await FutureDcursesRenderer.RefreshAsync( session );
	Require( output.Text == expected && output.FlushCount == 1, "Fresh baseline refresh must recover." );
	output.Clear();
	provider.SizeAvailable = false;
	Require( !session.GetDimensions().IsAvailable, "Fixture must expose unavailable dimensions." );
	Require( await ScreenOutputExample.DrawFrameAsync( session, "sample" ), "Sample must execute its supported frame." );
	Require( output.Text == "<reset><cup:0,0><bold>sample<reset>" && output.FlushCount == 1,
		"Sample must emit baseline, cursor, rendition, text, and reset in one commit." );
	output.Clear();
	Require( await ScreenOutputExample.DemonstrateStaleRecoveryAsync( session ), "Sample must recover using fresh work." );
	Require( output.Text == "Intervening output.\r\n<reset><cup:0,0><bold>Fresh frame after stale rejection.<reset>",
		"Recovery must omit the stale payload and emit one freshly planned frame." );
	output.Clear();
	using CancellationTokenSource cancelled = new();
	cancelled.Cancel();
	try {
		await ScreenOutputExample.DemonstrateStaleRecoveryAsync( session, cancelled.Token );
		throw new InvalidOperationException( "Pre-cancelled recovery must not run." );
	} catch ( OperationCanceledException ) {
		Require( output.Text.Length == 0, "Pre-cancelled recovery must emit nothing." );
	}
	output.FailNextWrite = true;
	try {
		await ScreenOutputExample.DemonstrateStaleRecoveryAsync( session );
		throw new InvalidOperationException( "Recovery must propagate a transport failure." );
	} catch ( Exception exception ) when ( exception is IOException or AggregateException ) {
		Require( output.Text == "Intervening output.\r\n", "Failed committed output must not be replayed." );
	}
}

// Execute the sample's real presentation and event path, including cleanup on failure.
foreach ( string scenario in new[] { "q", "escape", "eof", "no-presentation", "no-baseline", "failure", "cancel" } ) {
	Console.WriteLine( $"Screen sample host: {scenario}" );
	RecordingOutput output = new();
	TerminalDescriptionBuilder builder = new( "sample-host" );
	builder.SetString( StringCapability.CursorAddress, "<cup:%p1%d,%p2%d>" );
	builder.SetString( StringCapability.EnterBoldMode, "<bold>" );
	if ( scenario != "no-baseline" ) {
		builder.SetString( StringCapability.ExitAttributeMode, "<reset>" );
	}
	if ( scenario != "no-presentation" ) {
		builder.SetString( StringCapability.EnterCursorAddressingMode, "<enter>" );
		builder.SetString( StringCapability.ExitCursorAddressingMode, "<leave>" );
	}
	using CancellationTokenSource timeout = new( TimeSpan.FromSeconds( 5 ) );
	using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource( timeout.Token );
	if ( scenario == "failure" ) {
		output.FailOnText = "Terminal-owned screen output";
	}
	if ( scenario == "cancel" ) {
		output.AfterWrite = text => { if ( text.Contains( "Press q", StringComparison.Ordinal ) ) cancellation.Cancel(); };
	}
	await using TerminalSession session = await TerminalSession.OpenAsync(
		new TestControlProvider { SizeAvailable = false }, TerminalEndpoint.StandardInput,
		TerminalEndpoint.StandardOutput,
		new SampleInput( scenario == "escape" ? "\u001b" : scenario == "q" ? "q" : "" ), output,
		new TerminalSessionOptions { TerminalOverride = builder.Build(), ConfigureOutput = false, ObserveLifecycleEvents = false }
	);
	bool expectedFailure = false;
	try {
		bool available = await ScreenOutputExample.RunInteractiveAsync( session, scenario == "escape", cancellation.Token );
		Require( available == ( scenario is not "no-presentation" and not "no-baseline" ), "Unexpected sample availability." );
	} catch ( OperationCanceledException ) when ( scenario == "cancel" && !timeout.IsCancellationRequested ) {
		expectedFailure = true;
	} catch ( Exception exception ) when ( scenario == "failure" && exception is IOException or AggregateException ) {
		expectedFailure = true;
	}
	Require( expectedFailure == ( scenario is "failure" or "cancel" ), "Expected the sample failure to propagate." );
	if ( scenario == "no-presentation" ) {
		Require( output.Text.Length == 0, "Unavailable presentation must not draw on the caller's screen." );
	} else {
		Require( output.Text.StartsWith( "<enter>", StringComparison.Ordinal )
			&& output.Text.EndsWith( "<leave>", StringComparison.Ordinal ), "Sample must release alternate-screen ownership." );
		Require( !output.Text.Contains( "This stale frame", StringComparison.Ordinal ), "Stale sample payload leaked." );
		if ( scenario == "no-baseline" ) {
			Require( output.Text == "<enter><leave>", "Missing baseline must not emit a frame inside the presentation scope." );
		}
	}
}
// Published decoupled renderer, independently of the controlled compile boundary.
RecordingOutput cursesOutput = new();
TestControlProvider cursesProvider = new();
await using ( TerminalSession terminal = await TerminalSession.OpenAsync(
	cursesProvider, TerminalEndpoint.StandardInput, TerminalEndpoint.StandardOutput,
	new EmptyInput(), cursesOutput, new TerminalSessionOptions {
		TerminalOverride = new TerminalDescriptionBuilder( "dcurses-2.2-witness" )
			.SetString( StringCapability.CursorAddress, "<cup:%p1%d,%p2%d>" )
			.SetString( StringCapability.EnterBoldMode, "<bold>" )
			.SetString( StringCapability.ExitAttributeMode, "<reset>" )
			.Build(),
		ConfigureOutput = false, ObserveLifecycleEvents = false
	}
) ) {
	await using CursesSession curses = await CursesSession.OpenAsync( terminal,
		new CursesSessionOptions {
			UseAlternateScreen = false, EnableKeypad = false, HideCursor = false,
			UseSynchronizedOutput = true
		} );
	curses.StandardScreen.Write( "initial" );
	await curses.RefreshAsync();
	Require( cursesOutput.Text.Contains( "initial", StringComparison.Ordinal ), "DCurses initial refresh did not emit text." );
	cursesOutput.Clear();
	await curses.RefreshAsync();
	Require( cursesOutput.Text.Length == 0, "Unchanged DCurses refresh must emit nothing." );
	curses.Screen.VirtualScreen[ 0, 0 ] = new CursesCell( "X", new CursesStyle(
		CursesColor.Default, CursesColor.Default, CursesTextAttributes.Bold
	) );
	await curses.RefreshAsync();
	Require( cursesOutput.Text.Contains( "<bold>X", StringComparison.Ordinal ),
		"DCurses changed rendition/text must use the semantic planner." );
	for ( int iteration = 0; iteration < 16; ++iteration ) {
		cursesProvider.Size = new TerminalSize( 80 + iteration % 3, 24 + iteration % 2 );
		curses.Invalidate();
		cursesOutput.Clear();
		if ( 0 == iteration % 4 ) {
			cursesOutput.FailNextWrite = true;
			bool failed = false;
			try {
				await curses.RefreshAsync();
			} catch ( Exception exception ) when ( exception is IOException or AggregateException ) {
				failed = true;
			}
			Require( failed, "DCurses must surface the committed transport failure." );
			curses.Invalidate();
			cursesOutput.Clear();
		}
		await curses.RefreshAsync();
		Require( curses.Screen.Columns == cursesProvider.Size.Columns
			&& curses.Screen.Rows == cursesProvider.Size.Rows,
			"DCurses must reconcile live resize through Terminal dimensions." );
		Require( cursesOutput.Text.Contains( "<bold>X", StringComparison.Ordinal )
			&& cursesOutput.Text.Contains( "nitial", StringComparison.Ordinal ),
			"Invalidated DCurses refresh must repaint retained text." );
	}
}
Console.WriteLine( "Executed Terminal-only refresh/recovery and published DCurses 2.2.0 refresh/resize/repaint/close." );

static void Require( bool condition, string message ) {
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

internal sealed class EmptyInput : ITerminalInput {
	public ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult( 0 );
	}
}

internal sealed class RecordingOutput : ITerminalOutput {
	private readonly List<byte> bytes = [];
	internal string Text => Encoding.UTF8.GetString( this.bytes.ToArray() );
	internal int FlushCount { get; private set; }
	internal bool FailNextWrite { get; set; }
	internal string? FailOnText { get; set; }
	internal Action<string>? AfterWrite { get; set; }
	internal void Clear() { this.bytes.Clear(); this.FlushCount = 0; }
	public ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		this.bytes.AddRange( buffer.ToArray() );
		string text = Encoding.UTF8.GetString( buffer.Span );
		this.AfterWrite?.Invoke( text );
		if ( this.FailNextWrite || ( this.FailOnText is not null && text.Contains( this.FailOnText, StringComparison.Ordinal ) ) ) {
			this.FailNextWrite = false;
			this.FailOnText = null;
			throw new IOException( "Synthetic committed write failure." );
		}
		return ValueTask.CompletedTask;
	}
	public ValueTask FlushAsync( CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		++this.FlushCount;
		return ValueTask.CompletedTask;
	}
}

internal sealed class SampleInput( string text ) : ITerminalInput {
	private readonly byte[] bytes = Encoding.UTF8.GetBytes( text );
	private int position;
	public async ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		int length = Math.Min( buffer.Length, this.bytes.Length - this.position );
		this.bytes.AsMemory( this.position, length ).CopyTo( buffer );
		this.position += length;
		if ( length == 0 && this.bytes.Length > 0 ) {
			// Keep key fixtures open: ignoring q/Escape must time out, not pass via EOF.
			await Task.Delay( Timeout.InfiniteTimeSpan, cancellationToken );
		}
		return length;
	}
}

internal sealed class TestControlProvider : ITerminalControlProvider {
	internal TerminalSize Size { get; set; } = new( 80, 24 );
	internal bool SizeAvailable { get; set; } = true;
	private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
		0, 0, 0, 0x0002UL, new byte[32], 0, 32, 0,
		new TerminalSpeed(13, 9600), new TerminalSpeed(13, 9600)
	);
	public TerminalControlResult<TerminalEndpointObservation> Observe( TerminalEndpoint endpoint ) =>
		TerminalControlResult<TerminalEndpointObservation>.Available( new TerminalEndpointObservation(
			true, null, TerminalPlatformKind.PosixTermios,
			TerminalControlCapabilities.Attachment | TerminalControlCapabilities.ModeRead
				| TerminalControlCapabilities.ModeWrite | TerminalControlCapabilities.LiveSize
		) );
	public TerminalControlResult<TerminalModeSnapshot> GetMode( TerminalEndpoint endpoint ) =>
		TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
	public TerminalControlResult<TerminalSize> GetSize( TerminalEndpoint endpoint ) =>
		this.SizeAvailable ? TerminalControlResult<TerminalSize>.Available( this.Size )
			: TerminalControlResult<TerminalSize>.Unavailable( "No live size in this fixture." );
	public TerminalControlMutationResult SetMode( TerminalEndpoint endpoint,
		TerminalModeSnapshot mode, TerminalModeApplyTiming timing ) => TerminalControlMutationResult.Success();
}
