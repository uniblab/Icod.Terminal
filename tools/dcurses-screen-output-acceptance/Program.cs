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
	TerminalDescriptionBuilder builder = new( "screen-output-witness" );
	builder.SetString( StringCapability.CursorAddress, "<cup:%p1%d,%p2%d>" );
	builder.SetString( StringCapability.EnterBoldMode, "<bold>" );
	if ( recoverable ) {
		builder.SetString( StringCapability.ExitAttributeMode, "<reset>" );
	}
	await using TerminalSession session = await TerminalSession.OpenAsync(
		new TestControlProvider(), TerminalEndpoint.StandardInput,
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
	Require( await ScreenOutputExample.DrawFrameAsync( session, "sample" ), "Sample must execute its supported frame." );
	Require( output.Text == "<reset><cup:0,0><bold>sample<reset>" && output.FlushCount == 1,
		"Sample must emit baseline, cursor, rendition, text, and reset in one commit." );
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
	internal void Clear() { this.bytes.Clear(); this.FlushCount = 0; }
	public ValueTask WriteAsync( ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default ) {
		cancellationToken.ThrowIfCancellationRequested();
		this.bytes.AddRange( buffer.ToArray() );
		if ( this.FailNextWrite ) {
			this.FailNextWrite = false;
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

internal sealed class TestControlProvider : ITerminalControlProvider {
	internal TerminalSize Size { get; set; } = new( 80, 24 );
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
		TerminalControlResult<TerminalSize>.Available( this.Size );
	public TerminalControlMutationResult SetMode( TerminalEndpoint endpoint,
		TerminalModeSnapshot mode, TerminalModeApplyTiming timing ) => TerminalControlMutationResult.Success();
}
