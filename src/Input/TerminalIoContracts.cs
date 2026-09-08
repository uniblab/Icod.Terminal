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

/// <summary>
/// Supplies raw terminal input bytes to higher-level terminal consumers.
/// </summary>
public interface ITerminalInput {
	/// <summary>
	/// Reads terminal input bytes asynchronously.
	/// </summary>
	/// <param name="buffer">The destination buffer.</param>
	/// <param name="cancellationToken">Cancellation for the read operation.</param>
	/// <returns>
	/// The number of bytes read, or zero when the input endpoint has reached end of input.
	/// </returns>
	ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	);
}

/// <summary>
/// Receives terminal output bytes emitted by higher-level terminal consumers.
/// </summary>
public interface ITerminalOutput {
	/// <summary>Writes terminal output bytes asynchronously.</summary>
	/// <param name="buffer">The terminal output bytes.</param>
	/// <param name="cancellationToken">Cancellation for the write operation.</param>
	/// <returns>A value task representing the write operation.</returns>
	ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	);

	/// <summary>Flushes buffered terminal output asynchronously.</summary>
	/// <param name="cancellationToken">Cancellation for the flush operation.</param>
	/// <returns>A value task representing the flush operation.</returns>
	ValueTask FlushAsync(
		CancellationToken cancellationToken = default
	);
}

/// <summary>
/// Adapts a borrowed readable stream to <see cref="ITerminalInput"/>.
/// </summary>
internal sealed class StreamTerminalInput : ITerminalInput {
	private readonly Stream stream;

	internal StreamTerminalInput(
		Stream stream
	) {
		ArgumentNullException.ThrowIfNull( stream );
		if ( !stream.CanRead ) {
			throw new ArgumentException(
				"A terminal input stream must be readable.",
				nameof( stream )
			);
		}

		this.stream = stream;
	}

	public ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		return this.stream.ReadAsync(
			buffer,
			cancellationToken
		);
	}
}

/// <summary>
/// Adapts a borrowed writable stream to <see cref="ITerminalOutput"/>.
/// </summary>
internal sealed class StreamTerminalOutput : ITerminalOutput {
	private readonly Stream stream;

	internal StreamTerminalOutput(
		Stream stream
	) {
		ArgumentNullException.ThrowIfNull( stream );
		if ( !stream.CanWrite ) {
			throw new ArgumentException(
				"A terminal output stream must be writable.",
				nameof( stream )
			);
		}

		this.stream = stream;
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		return this.stream.WriteAsync(
			buffer,
			cancellationToken
		);
	}

	public ValueTask FlushAsync(
		CancellationToken cancellationToken = default
	) {
		return new ValueTask(
			this.stream.FlushAsync( cancellationToken )
		);
	}
}
