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

using System.Buffers;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

/// <summary>
/// Reads uninterpreted bytes from an independently opened Unix terminal handle.
/// Console.OpenStandardInput uses managed line editing on interactive Unix stdin.
/// </summary>
internal sealed class PosixTerminalInput : ITerminalInput, IDisposable {
	private readonly object sync = new();
	private readonly SafeFileHandle handle;
	private bool disposed;

	private PosixTerminalInput( SafeFileHandle handle ) {
		this.handle = handle;
	}

	internal static PosixTerminalInput OpenStandardInput() {
		if ( !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() ) {
			throw new PlatformNotSupportedException( "Direct POSIX terminal input requires Linux or macOS." );
		}
		TerminalEndpointObservation observation = SystemTerminalControlProvider.Instance
			.Observe( TerminalEndpoint.StandardInput ).GetRequiredValue();
		if ( !observation.IsTerminal || observation.Pathname is null ) {
			throw new InvalidOperationException( "Standard input must identify an interactive terminal device." );
		}

		// A dup shares file status flags with stdin. Reopen its observed terminal
		// instead, so O_NONBLOCK cannot change the shell's descriptor flags.
		// O_NOCTTY prevents acquiring a controlling terminal; O_CLOEXEC prevents
		// leaking our owned descriptor to a subsequently launched child.
		int flags = OperatingSystem.IsLinux()
			? 0x100 | 0x800 | 0x80000
			: 0x20000 | 0x4 | 0x1000000;
		int descriptor = NativeOpen( observation.Pathname, flags );
		if ( descriptor < 0 ) {
			throw NativeError( "open terminal input", Marshal.GetLastPInvokeError() );
		}
		return new PosixTerminalInput( new SafeFileHandle( (nint)descriptor, ownsHandle: true ) );
	}

	public async ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		lock ( this.sync ) {
			ObjectDisposedException.ThrowIf( this.disposed, this );
		}
		if ( buffer.IsEmpty ) {
			return 0;
		}

		int capacity = Math.Min( buffer.Length, 4096 );
		byte[] bytes = ArrayPool<byte>.Shared.Rent( capacity );
		try {
			while ( true ) {
				cancellationToken.ThrowIfCancellationRequested();
				nint count;
				int error;
				lock ( this.sync ) {
					ObjectDisposedException.ThrowIf( this.disposed, this );
					// Always nonblocking. Disposal uses the same lock, so no read can
					// begin after the descriptor is closed or accidentally use a reused fd.
					count = NativeRead( this.handle, bytes, (nuint)capacity );
					error = count < 0 ? Marshal.GetLastPInvokeError() : 0;
				}
				if ( count >= 0 ) {
					bytes.AsMemory( 0, checked( (int)count ) ).CopyTo( buffer );
					return (int)count;
				}
				if ( error == 4 ) { // EINTR: retry after checking cancellation.
					continue;
				}
				if ( error != ( OperatingSystem.IsLinux() ? 11 : 35 ) ) { // EAGAIN/EWOULDBLOCK
					throw NativeError( "read terminal input", error );
				}
				// No blocked worker, shared-fd mutation, or abandoned native read.
				// Only an outstanding read polls; cancellation interrupts this wait.
				await Task.Delay( 10, cancellationToken ).ConfigureAwait( false );
			}
		} finally {
			ArrayPool<byte>.Shared.Return( bytes );
		}
	}

	public void Dispose() {
		lock ( this.sync ) {
			if ( this.disposed ) {
				return;
			}
			this.disposed = true;
			this.handle.Dispose();
		}
	}

	private static IOException NativeError( string operation, int error ) =>
		new( $"Cannot {operation}: {new Win32Exception( error ).Message} (errno {error})." );

#pragma warning disable SYSLIB1054 // Match the existing Linux/macOS terminal interop boundary.
	[DllImport( "libc", EntryPoint = "open", SetLastError = true )]
	private static extern int NativeOpen( [MarshalAs( UnmanagedType.LPUTF8Str )] string path, int flags );

	[DllImport( "libc", EntryPoint = "read", SetLastError = true )]
	private static extern nint NativeRead( SafeFileHandle descriptor, [Out] byte[] buffer, nuint count );
#pragma warning restore SYSLIB1054
}
