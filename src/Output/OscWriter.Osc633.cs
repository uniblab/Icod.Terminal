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

internal static partial class OscWriter {
	private static readonly byte[] Osc633PromptStartFrame = TerminalOsc633Encoder.EncodeMarker( 'A' );
	private static readonly byte[] Osc633CommandInputStartFrame = TerminalOsc633Encoder.EncodeMarker( 'B' );
	private static readonly byte[] Osc633CommandOutputStartFrame = TerminalOsc633Encoder.EncodeMarker( 'C' );
	private static readonly byte[] Osc633CommandAbortedFrame = TerminalOsc633Encoder.EncodeMarker( 'D' );

	internal static byte[] EncodeOsc633PromptStartFrame() {
		return Osc633PromptStartFrame.ToArray();
	}

	internal static byte[] EncodeOsc633CommandInputStartFrame() {
		return Osc633CommandInputStartFrame.ToArray();
	}

	internal static byte[] EncodeOsc633CommandOutputStartFrame() {
		return Osc633CommandOutputStartFrame.ToArray();
	}

	internal static byte[] EncodeOsc633CommandFinishedFrame(
		int exitCode
	) {
		return TerminalOsc633Encoder.EncodeCommandFinished( exitCode );
	}

	internal static byte[] EncodeOsc633CommandAbortedFrame() {
		return Osc633CommandAbortedFrame.ToArray();
	}

	internal static byte[] EncodeOsc633CommandLineFrame(
		string commandLine,
		string? nonce = null
	) {
		ArgumentNullException.ThrowIfNull( commandLine );
		return TerminalOsc633Encoder.EncodeCommandLine(
			commandLine,
			nonce
		);
	}

	internal static byte[] EncodeOsc633CurrentDirectoryFrame(
		string currentDirectory,
		string? nonce = null
	) {
		ArgumentNullException.ThrowIfNull( currentDirectory );
		return TerminalOsc633Encoder.EncodeCurrentDirectory(
			currentDirectory,
			nonce
		);
	}

	internal static byte[] EncodeOsc633IsWindowsFrame(
		bool isWindows
	) {
		return TerminalOsc633Encoder.EncodeIsWindows( isWindows );
	}

	internal static byte[] EncodeOsc633ContinuationPromptFrame(
		string continuationPrompt
	) {
		ArgumentNullException.ThrowIfNull( continuationPrompt );
		return TerminalOsc633Encoder.EncodeContinuationPrompt( continuationPrompt );
	}

	internal static byte[] EncodeOsc633RichCommandDetectionFrame(
		bool hasRichCommandDetection
	) {
		return TerminalOsc633Encoder.EncodeRichCommandDetection(
			hasRichCommandDetection
		);
	}

	internal static ValueTask WriteOsc633PromptStartAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc633FrameAsync(
			output,
			Osc633PromptStartFrame,
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc633CommandInputStartAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc633FrameAsync(
			output,
			Osc633CommandInputStartFrame,
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc633CommandOutputStartAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc633FrameAsync(
			output,
			Osc633CommandOutputStartFrame,
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc633CommandFinishedAsync(
		ITerminalOutput output,
		int exitCode,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc633FrameAsync(
			output,
			EncodeOsc633CommandFinishedFrame( exitCode ),
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc633CommandAbortedAsync(
		ITerminalOutput output,
		CancellationToken cancellationToken = default
	) {
		return WriteOsc633FrameAsync(
			output,
			Osc633CommandAbortedFrame,
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc633CommandLineAsync(
		ITerminalOutput output,
		string commandLine,
		string? nonce = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( commandLine );
		cancellationToken.ThrowIfCancellationRequested();
		return WriteOsc633FrameAsync(
			output,
			EncodeOsc633CommandLineFrame(
				commandLine,
				nonce
			),
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc633CurrentDirectoryAsync(
		ITerminalOutput output,
		string currentDirectory,
		string? nonce = null,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( currentDirectory );
		cancellationToken.ThrowIfCancellationRequested();
		return WriteOsc633FrameAsync(
			output,
			EncodeOsc633CurrentDirectoryFrame(
				currentDirectory,
				nonce
			),
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc633IsWindowsAsync(
		ITerminalOutput output,
		bool isWindows,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();
		return WriteOsc633FrameAsync(
			output,
			EncodeOsc633IsWindowsFrame( isWindows ),
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc633ContinuationPromptAsync(
		ITerminalOutput output,
		string continuationPrompt,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		ArgumentNullException.ThrowIfNull( continuationPrompt );
		cancellationToken.ThrowIfCancellationRequested();
		return WriteOsc633FrameAsync(
			output,
			EncodeOsc633ContinuationPromptFrame( continuationPrompt ),
			cancellationToken
		);
	}

	internal static ValueTask WriteOsc633RichCommandDetectionAsync(
		ITerminalOutput output,
		bool hasRichCommandDetection,
		CancellationToken cancellationToken = default
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();
		return WriteOsc633FrameAsync(
			output,
			EncodeOsc633RichCommandDetectionFrame( hasRichCommandDetection ),
			cancellationToken
		);
	}

	private static async ValueTask WriteOsc633FrameAsync(
		ITerminalOutput output,
		ReadOnlyMemory<byte> frame,
		CancellationToken cancellationToken
	) {
		ArgumentNullException.ThrowIfNull( output );
		cancellationToken.ThrowIfCancellationRequested();
		if ( frame.IsEmpty ) {
			throw new ArgumentException(
				"An OSC 633 frame cannot be empty.",
				nameof( frame )
			);
		}
		cancellationToken.ThrowIfCancellationRequested();

		await output.WriteAsync(
			frame,
			CancellationToken.None
		).ConfigureAwait( false );
	}
}
