/*
	Icod.Terminal.WindowsInput.Smoke
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
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Icod.Terminal;
using Icod.TermInfo;
using Microsoft.Win32.SafeHandles;

// Run in a separate process: a private console must never alter the CI runner's console.
if ( !OperatingSystem.IsWindows() ) return 0;
Native.FreeConsole();
Check( Native.AllocConsole(), "AllocConsole" );
using SafeFileHandle handle = Native.CreateFile( "CONIN$", 0xc0000000, 3, 0, 3, 0, 0 );
Check( !handle.IsInvalid, "CONIN$" );
using SafeFileHandle outputHandle = Native.CreateFile( "CONOUT$", 0xc0000000, 3, 0, 3, 0, 0 );
Check( !outputHandle.IsInvalid, "CONOUT$" );
Check( Native.SetStdHandle( -10, handle.DangerousGetHandle() ), "SetStdHandle" );
Check( Native.SetStdHandle( -11, outputHandle.DangerousGetHandle() ), "SetStdHandle" );
Check( Native.GetConsoleMode( handle, out uint originalInputMode ), "GetConsoleMode" );
Check( Native.SetConsoleOutputCP( 437U ), "SetConsoleOutputCP" );
using CancellationTokenSource deadline = new( TimeSpan.FromSeconds( 20 ) );
await using ( TerminalSession session = await TerminalSession.OpenAsync( new TerminalSessionOptions {
	InputMode = TerminalInputMode.Raw, EchoInput = false,
	ObserveLifecycleEvents = false, TerminalOverride = TerminalProfiles.Xterm
}, deadline.Token ) ) {
	if ( 65001U != Native.GetConsoleOutputCP() ) throw new InvalidOperationException( "Windows console output was not configured for UTF-8." );
	Check( Native.GetConsoleMode( handle, out uint inputMode ), "GetConsoleMode" );
	if ( 0x0290U != ( inputMode & 0x0290U ) || 0U != ( inputMode & 0x0040U ) ) throw new InvalidOperationException( "Raw mode did not own VT and mouse input while suppressing Quick Edit." );
	Check( Native.GetConsoleScreenBufferInfo( outputHandle, out Native.ScreenBufferInfo screen ), "GetConsoleScreenBufferInfo" );
	await session.WriteTextAsync( "─", deadline.Token );
	await session.Output.FlushAsync( deadline.Token );
	char[] cell = new char[ 1 ];
	Check( Native.ReadConsoleOutputCharacter( outputHandle, cell, 1, screen.CursorPosition, out uint cellsRead ) && 1U == cellsRead, "ReadConsoleOutputCharacter" );
	if ( '─' != cell[ 0 ] ) throw new InvalidOperationException( "UTF-8 box drawing did not occupy the expected Windows console cell." );
	Send( '\u0003', 0x43, 8 ); Send( '\u001a', 0x5a, 8 ); Send( 'x', 0x58 ); Send( '\u001b', 0x1b ); Send( 'f', 0x46 ); Send( '界', 0 );
	TerminalInputEvent copy = await Read( session, deadline.Token );
	if ( copy.Kind != TerminalInputEventKind.Key || copy.Character != new Rune( 'C' ) || copy.Modifiers != TerminalKeyModifiers.Control ) throw new InvalidOperationException( "Ctrl+C was not a control key." );
	TerminalInputEvent undo = await Read( session, deadline.Token );
	if ( undo.Kind != TerminalInputEventKind.Key || undo.Character != new Rune( 'Z' ) || undo.Modifiers != TerminalKeyModifiers.Control ) throw new InvalidOperationException( "Ctrl+Z was not a control key." );
	if ( ( await Read( session, deadline.Token ) ).Character != new Rune( 'x' ) ) throw new InvalidOperationException( "Input ended after Ctrl+Z." );
	TerminalInputEvent alt = await Read( session, deadline.Token );
	if ( alt.Character != new Rune( 'f' ) || alt.Modifiers != TerminalKeyModifiers.Alt ) throw new InvalidOperationException( "Alt+F was not preserved." );
	if ( ( await Read( session, deadline.Token ) ).Character != new Rune( '界' ) ) throw new InvalidOperationException( "Unicode input was not preserved." );
	// A pending native read must not hang disposal after the caller stops waiting.
	using CancellationTokenSource cancel = new( TimeSpan.FromMilliseconds( 100 ) );
	if ( ( await session.ReadEventAsync( cancel.Token ) ).Kind != TerminalEventKind.Cancelled ) throw new InvalidOperationException( "Expected canceled wait." );
}
if ( 437U != Native.GetConsoleOutputCP() ) throw new InvalidOperationException( "Windows console output code page was not restored." );
Check( Native.GetConsoleMode( handle, out uint restoredInputMode ), "GetConsoleMode" );
if ( originalInputMode != restoredInputMode ) throw new InvalidOperationException( "Windows console input mode was not restored exactly." );
return 0;

void Send( char value, ushort key, uint modifiers = 0 ) {
	Native.InputRecord[] records = [ new() { EventType = 1, KeyDown = 1, RepeatCount = 1, VirtualKey = key, Character = value, ControlState = modifiers } ];
	Check( Native.WriteConsoleInput( handle, records, 1, out uint written ) && written == 1, "WriteConsoleInput" );
}
static async Task<TerminalInputEvent> Read( TerminalSession session, CancellationToken token ) => ( await session.ReadEventAsync( token ) ).Input ?? throw new InvalidOperationException( "Expected input before deadline." );
static void Check( bool result, string operation ) { if ( !result ) throw new Win32Exception( Marshal.GetLastPInvokeError(), operation ); }

internal static class Native {
	[StructLayout( LayoutKind.Sequential )]
	internal struct Coordinate {
		internal short X;
		internal short Y;
	}

	[StructLayout( LayoutKind.Sequential )]
	internal struct SmallRectangle {
		internal short Left;
		internal short Top;
		internal short Right;
		internal short Bottom;
	}

	[StructLayout( LayoutKind.Sequential )]
	internal struct ScreenBufferInfo {
		internal Coordinate Size;
		internal Coordinate CursorPosition;
		internal ushort Attributes;
		internal SmallRectangle Window;
		internal Coordinate MaximumWindowSize;
	}

	[StructLayout( LayoutKind.Explicit, Size = 20, CharSet = CharSet.Unicode )]
	internal struct InputRecord {
		[FieldOffset( 0 )] public ushort EventType;
		[FieldOffset( 4 )] public int KeyDown;
		[FieldOffset( 8 )] public ushort RepeatCount;
		[FieldOffset( 10 )] public ushort VirtualKey;
		[FieldOffset( 14 )] public char Character;
		[FieldOffset( 16 )] public uint ControlState;
	}
#pragma warning disable SYSLIB1054
	[DllImport( "kernel32.dll", SetLastError = true )] [return: MarshalAs( UnmanagedType.Bool )] internal static extern bool AllocConsole();
	[DllImport( "kernel32.dll", SetLastError = true )] [return: MarshalAs( UnmanagedType.Bool )] internal static extern bool FreeConsole();
	[DllImport( "kernel32.dll", SetLastError = true )] [return: MarshalAs( UnmanagedType.Bool )] internal static extern bool SetStdHandle( int kind, nint handle );
	[DllImport( "kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true )] internal static extern SafeFileHandle CreateFile( string name, uint access, uint share, nint security, uint creation, uint flags, nint template );
	[DllImport( "kernel32.dll", SetLastError = true )] [return: MarshalAs( UnmanagedType.Bool )] internal static extern bool GetConsoleMode( SafeFileHandle handle, out uint mode );
	[DllImport( "kernel32.dll", SetLastError = true )] internal static extern uint GetConsoleOutputCP();
	[DllImport( "kernel32.dll", SetLastError = true )] [return: MarshalAs( UnmanagedType.Bool )] internal static extern bool SetConsoleOutputCP( uint codePage );
	[DllImport( "kernel32.dll", SetLastError = true )] [return: MarshalAs( UnmanagedType.Bool )] internal static extern bool GetConsoleScreenBufferInfo( SafeFileHandle handle, out ScreenBufferInfo info );
	[DllImport( "kernel32.dll", EntryPoint = "ReadConsoleOutputCharacterW", CharSet = CharSet.Unicode, SetLastError = true )] [return: MarshalAs( UnmanagedType.Bool )] internal static extern bool ReadConsoleOutputCharacter( SafeFileHandle handle, [Out] char[] buffer, uint length, Coordinate coordinate, out uint read );
	[DllImport( "kernel32.dll", EntryPoint = "WriteConsoleInputW", SetLastError = true )] [return: MarshalAs( UnmanagedType.Bool )] internal static extern bool WriteConsoleInput( SafeFileHandle handle, InputRecord[] records, uint count, out uint written );
#pragma warning restore SYSLIB1054
}
