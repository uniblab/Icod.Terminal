/*
	Icod.Terminal
	Managed, cross-platform live-terminal session and terminal-control library for .NET.
	Copyright (C) 2026  Timothy J. Bruce <uniblab@hotmail.com>
*/

/*
	This program is free software: you can redistribute it and/or modify
	it under the terms of the GNU Lesser General Public License as published by
	the Free Software Foundation, either version 3 of the License, or
	(at your option) any later version.

	This program is distributed in the hope that it will be useful,
	but WITHOUT ANY WARRANTY; without even the implied warranty of
	MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
	GNU Lesser General Public License for more details.

	You should have received a copy of the GNU Lesser General Public License
	along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/
namespace Icod.Terminal;

using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

/// <summary>Reads Windows console UTF-16 directly, preserving Ctrl+Z and encoding it as UTF-8.</summary>
internal sealed class WindowsTerminalInput : ITerminalInput, IDisposable {
	private readonly Func<char[], int> read;
	private readonly Action cancel;
	private readonly IDisposable? owner;
	private readonly CancellationTokenSource stop = new();
	private readonly SemaphoreSlim gate = new( 1, 1 );
	private readonly Encoder encoder = Encoding.UTF8.GetEncoder();
	private readonly char[] characters = new char[ 256 ];
	private readonly byte[] bytes = new byte[ Encoding.UTF8.GetMaxByteCount( 256 ) ];
	private int offset, count;
	private bool ended;
	private int disposed;

	internal WindowsTerminalInput( Func<char[], int> read, Action cancel, IDisposable? owner = null ) {
		this.read = read; this.cancel = cancel; this.owner = owner;
	}

	internal static WindowsTerminalInput OpenStandardInput() {
		if ( !OperatingSystem.IsWindows() ) throw new PlatformNotSupportedException( "Direct Windows console input requires Windows." );
		if ( !SystemTerminalControlProvider.Instance.Observe( TerminalEndpoint.StandardInput ).GetRequiredValue().IsTerminal )
			throw new InvalidOperationException( "Standard input must identify an interactive console." );
		// A separate console handle gives cancellation ownership to this reader alone.
		SafeFileHandle handle = CreateFile( "CONIN$", 0x80000000, 3, 0, 3, 0, 0 );
		if ( handle.IsInvalid ) { int error = Marshal.GetLastPInvokeError(); handle.Dispose(); throw NativeError( error ); }
		return new WindowsTerminalInput( buffer => {
			if ( !ReadConsole( handle, buffer, (uint)buffer.Length, out uint read, 0 ) ) throw NativeError( Marshal.GetLastPInvokeError() );
			return checked( (int)read );
		}, () => { _ = CancelIoEx( handle, 0 ); }, handle );
	}

	public async ValueTask<int> ReadAsync( Memory<byte> buffer, CancellationToken cancellationToken = default ) {
		ObjectDisposedException.ThrowIf( Volatile.Read( ref this.disposed ) != 0, this );
		using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource( cancellationToken, this.stop.Token );
		CancellationToken token = linked.Token;
		await this.gate.WaitAsync( token ).ConfigureAwait( false );
		try {
			token.ThrowIfCancellationRequested();
			if ( buffer.IsEmpty ) return 0;
			while ( this.count == 0 && !this.ended ) {
				TaskCompletionSource<bool> canceled = new( TaskCreationOptions.RunContinuationsAsynchronously );
				using CancellationTokenRegistration registration = token.Register( () => canceled.TrySetResult( true ) );
				Task<int> reading = Task.Run( () => this.read( this.characters ), CancellationToken.None );
				await Task.WhenAny( reading, canceled.Task ).ConfigureAwait( false );
				// Repeat until the native call ends: cancellation can race its initial entry.
				while ( !reading.IsCompleted ) {
					this.cancel(); await Task.WhenAny( reading, Task.Delay( 10 ) ).ConfigureAwait( false );
				}
				int read;
				try { read = await reading.ConfigureAwait( false ); }
				catch ( IOException ) when ( token.IsCancellationRequested ) { throw new OperationCanceledException( token ); }
				token.ThrowIfCancellationRequested();
				this.ended = read == 0; this.offset = 0;
				this.count = this.encoder.GetBytes( this.characters, 0, read, this.bytes, 0, flush: this.ended );
			}
			int copied = Math.Min( buffer.Length, this.count );
			this.bytes.AsMemory( this.offset, copied ).CopyTo( buffer ); this.offset += copied; this.count -= copied;
			return copied;
		} finally { this.gate.Release(); }
	}

	public void Dispose() {
		if ( Interlocked.Exchange( ref this.disposed, 1 ) != 0 ) return;
		this.stop.Cancel(); this.gate.Wait();
		try { this.owner?.Dispose(); } finally { this.gate.Release(); }
	}

	private static IOException NativeError( int error ) => new( $"Cannot read Windows console input: {new Win32Exception( error ).Message} (error {error})." );
#pragma warning disable SYSLIB1054 // Match the existing platform interop boundary.
	[DllImport( "kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true )]
	private static extern SafeFileHandle CreateFile( string name, uint access, uint share, nint security, uint creation, uint flags, nint template );
	[DllImport( "kernel32.dll", EntryPoint = "ReadConsoleW", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool ReadConsole( SafeFileHandle handle, [Out, MarshalAs( UnmanagedType.LPArray, ArraySubType = UnmanagedType.U2, SizeParamIndex = 2 )] char[] buffer, uint length, out uint read, nint control );
	[DllImport( "kernel32.dll", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool CancelIoEx( SafeFileHandle handle, nint overlapped );
#pragma warning restore SYSLIB1054
}
