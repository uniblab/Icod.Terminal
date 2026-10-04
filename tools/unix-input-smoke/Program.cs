/*
	Icod.Terminal.UnixInputSmoke
	Validation utility for Icod.Terminal release and integration contracts.
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
using System.Diagnostics;
using System.Reflection;
using System.Text;
using Icod.Terminal;

if ( OperatingSystem.IsWindows() ) {
	Console.WriteLine( "Unix standard-input regression: not applicable on Windows." );
	return 0;
}
if ( args.Length > 0 && args[0] == "--child" ) {
	try {
		await RunChildAsync( Enum.Parse<TerminalInputMode>( args[1] ) );
		return 0;
	} catch ( Exception exception ) {
		File.WriteAllText( args[2], exception.ToString() );
		return 1;
	}
}
foreach ( TerminalInputMode mode in new[] { TerminalInputMode.CBreak, TerminalInputMode.Raw } ) {
	await RunParentAsync( mode );
}
Console.WriteLine( "Unix standard-input regression passed: immediate keys, UTF-8, query routing, no echo, cancellation, restoration and reopen." );
return 0;

static async Task RunChildAsync( TerminalInputMode mode ) {
	var provider = SystemTerminalControlProvider.Instance;
	var baseline = provider.GetMode( TerminalEndpoint.StandardInput ).GetRequiredValue();
	TerminalSession session = await TerminalSession.OpenAsync( new TerminalSessionOptions { InputMode = mode } );
	await session.WriteTextAsync( "READY|" );
	await ExpectCharacterAsync( session, 'd' );
	await session.WriteTextAsync( "KEY|" );
	await ExpectCharacterAsync( session, 'é' );
	await session.WriteTextAsync( "UTF8|" );
	_ = await session.QueryPrimaryDeviceAttributesAsync( TimeSpan.FromSeconds( 3 ) );
	await session.WriteTextAsync( "QUERY|" );
	using ( CancellationTokenSource cancellation = new( TimeSpan.FromMilliseconds( 100 ) ) ) {
		TerminalEvent result = await session.ReadEventAsync( cancellation.Token );
		Require( result.Kind == TerminalEventKind.Cancelled, "Idle input wait did not cancel." );
	}
	await session.DisposeAsync().AsTask().WaitAsync( TimeSpan.FromSeconds( 3 ) );
	var restored = provider.GetMode( TerminalEndpoint.StandardInput ).GetRequiredValue();
	Require( baseline.InputFlags == restored.InputFlags && baseline.OutputFlags == restored.OutputFlags
		&& baseline.ControlFlags == restored.ControlFlags && baseline.LocalFlags == restored.LocalFlags
		&& baseline.ControlCharacters.SequenceEqual( restored.ControlCharacters ),
		$"Terminal mode was not restored. Before: {Describe( baseline )}; after: {Describe( restored )}" );
	// The cancelled wait must not leave a reader stealing the next session's input.
	await using ( TerminalSession reopened = await TerminalSession.OpenAsync() ) {
		await reopened.WriteTextAsync( "REOPEN|" );
		await ExpectCharacterAsync( reopened, 'q' );
	}
	Console.Write( "PASS|" );
}

static async Task ExpectCharacterAsync( TerminalSession session, char expected ) {
	TerminalEvent result = await session.ReadEventAsync( TimeSpan.FromSeconds( 3 ) );
	Require( result.Input?.Character?.Value == expected,
		$"Expected immediate '{expected}' without Enter; received {result.Kind}." );
}

static async Task RunParentAsync( TerminalInputMode mode ) {
	string assembly = Assembly.GetExecutingAssembly().Location;
	string failurePath = Path.GetTempFileName();
	ProcessStartInfo start = new( "script" ) {
		RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
		UseShellExecute = false
	};
	start.Environment["TERM"] = "xterm";
	start.ArgumentList.Add( "-q" );
	if ( OperatingSystem.IsLinux() ) {
		start.ArgumentList.Add( "-e" );
		start.ArgumentList.Add( "-c" );
		start.ArgumentList.Add( "dotnet " + Quote( assembly ) + " --child " + mode + " " + Quote( failurePath ) );
		start.ArgumentList.Add( "/dev/null" );
	} else {
		start.ArgumentList.Add( "/dev/null" );
		start.ArgumentList.Add( "dotnet" );
		start.ArgumentList.Add( assembly );
		start.ArgumentList.Add( "--child" );
		start.ArgumentList.Add( mode.ToString() );
		start.ArgumentList.Add( failurePath );
	}
	using Process process = Process.Start( start ) ?? throw new InvalidOperationException( "Cannot start script." );
	Task<string> errors = process.StandardError.ReadToEndAsync();
	using CancellationTokenSource deadline = new( TimeSpan.FromSeconds( 20 ) );
	StringBuilder output = new();
	try {
		// Console initialization may emit keypad setup before application input.
		// Establish the boundary first; every byte after it is checked exactly.
		while ( !output.ToString().EndsWith( "READY|", StringComparison.Ordinal ) ) {
			char[] initial = new char[1];
			int count = await process.StandardOutput.ReadAsync( initial.AsMemory(), deadline.Token );
			Require( count == 1, "Child exited before READY." );
			output.Append( initial[0] );
			Require( output.Length < 4096, "Unexpectedly large startup output." );
		}
		await SendAsync( "d" );
		await ExpectAsync( "KEY|" );
		// Split a UTF-8 scalar across writes to exercise the byte decoder.
		await process.StandardInput.BaseStream.WriteAsync( new byte[] { 0xc3 }, deadline.Token );
		await process.StandardInput.BaseStream.FlushAsync( deadline.Token );
		await Task.Delay( 30, deadline.Token );
		await process.StandardInput.BaseStream.WriteAsync( new byte[] { 0xa9 }, deadline.Token );
		await process.StandardInput.BaseStream.FlushAsync( deadline.Token );
		await ExpectAsync( "UTF8|\u001b[c" );
		await SendAsync( "\u001b[?1;2c" );
		await ExpectAsync( "QUERY|REOPEN|" );
		await SendAsync( "q" );
		await ExpectAsync( "PASS|" );
		process.StandardInput.Close();
		await process.WaitForExitAsync( deadline.Token );
		Require( process.ExitCode == 0, $"Child exited {process.ExitCode}: {await errors}" );
		Console.WriteLine( $"{mode}: passed" );
	} catch ( Exception exception ) {
		throw new InvalidOperationException( $"Unix input ({mode}) failed. Captured output: {output}. Child failure: {File.ReadAllText( failurePath )}", exception );
	} finally {
		if ( !process.HasExited ) {
			process.Kill( entireProcessTree: true );
			await process.WaitForExitAsync();
		}
		File.Delete( failurePath );
	}

	async Task SendAsync( string text ) {
		await process.StandardInput.BaseStream.WriteAsync( Encoding.UTF8.GetBytes( text ), deadline.Token );
		await process.StandardInput.BaseStream.FlushAsync( deadline.Token );
	}
	async Task ExpectAsync( string expected ) {
		// Exact output is deliberate: even one echoed key or response is a failure.
		foreach ( char character in expected ) {
			char[] buffer = new char[1];
			int count = await process.StandardOutput.ReadAsync( buffer.AsMemory(), deadline.Token );
			Require( count == 1, "Child exited before producing " + expected );
			output.Append( buffer[0] );
			Require( buffer[0] == character, $"Unexpected output (possible input echo): U+{(int)buffer[0]:X4}, expected U+{(int)character:X4}." );
		}
	}
}

static string Quote( string value ) => "'" + value.Replace( "'", "'\"'\"'", StringComparison.Ordinal ) + "'";
static string Describe( TerminalModeSnapshot mode ) =>
	$"iflag={mode.InputFlags:X}, oflag={mode.OutputFlags:X}, cflag={mode.ControlFlags:X}, lflag={mode.LocalFlags:X}, cc={Convert.ToHexString( mode.ControlCharacters.ToArray() )}";
static void Require( bool condition, string message ) {
	if ( !condition ) throw new InvalidOperationException( message );
}
