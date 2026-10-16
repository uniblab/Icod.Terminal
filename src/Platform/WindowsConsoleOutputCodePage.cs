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

using System.Runtime.InteropServices;

/// <summary>
/// Acquires reversible UTF-8 decoding for byte-oriented Windows console output.
/// </summary>
internal static class WindowsConsoleOutputCodePage {
	private const uint Utf8CodePage = 65001U;

	internal static IDisposable? TryEnableUtf8() {
		if ( !OperatingSystem.IsWindows() ) return null;
		return TryEnableUtf8( GetConsoleOutputCP, SetConsoleOutputCP );
	}

	internal static IDisposable? TryEnableUtf8(
		Func<uint> getOutputCodePage,
		Func<uint, bool> setOutputCodePage
	) {
		ArgumentNullException.ThrowIfNull( getOutputCodePage );
		ArgumentNullException.ThrowIfNull( setOutputCodePage );

		uint originalCodePage = getOutputCodePage();
		if ( 0U == originalCodePage ) return null;
		bool restore = Utf8CodePage != originalCodePage;
		if ( restore && !setOutputCodePage( Utf8CodePage ) ) return null;
		return new Lease( setOutputCodePage, originalCodePage, restore );
	}

	private sealed class Lease(
		Func<uint, bool> setOutputCodePage,
		uint originalCodePage,
		bool restore
	) : IDisposable {
		private Func<uint, bool>? setOutputCodePage = setOutputCodePage;

		public void Dispose() {
			Func<uint, bool>? setter = Interlocked.Exchange(
				ref this.setOutputCodePage,
				null
			);
			if ( setter is null || !restore ) return;
			_ = setter( originalCodePage );
		}
	}

#pragma warning disable SYSLIB1054 // Match the existing platform interop boundary.
	[DllImport( "kernel32.dll", SetLastError = true )]
	private static extern uint GetConsoleOutputCP();

	[DllImport( "kernel32.dll", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool SetConsoleOutputCP( uint codePage );
#pragma warning restore SYSLIB1054
}
