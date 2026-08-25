namespace Icod.Terminal;

/// <summary>
/// Adapts a borrowed <see cref="ITerminalOutput"/> to the asynchronous stream
/// surface consumed by <c>Icod.TermInfo</c> output helpers.
/// </summary>
internal sealed class TerminalOutputStream : Stream {
	private readonly ITerminalOutput output;

	internal TerminalOutputStream(
		ITerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( output );
		this.output = output;
	}

	public override bool CanRead => false;

	public override bool CanSeek => false;

	public override bool CanWrite => true;

	public override long Length => throw new NotSupportedException();

	public override long Position {
		get => throw new NotSupportedException();
		set => throw new NotSupportedException();
	}

	public override void Flush() {
		throw new NotSupportedException(
			"Synchronous terminal output is not supported by this adapter."
		);
	}

	public override Task FlushAsync(
		CancellationToken cancellationToken
	) {
		return this.output.FlushAsync( cancellationToken ).AsTask();
	}

	public override int Read(
		byte[] buffer,
		int offset,
		int count
	) {
		throw new NotSupportedException();
	}

	public override long Seek(
		long offset,
		SeekOrigin origin
	) {
		throw new NotSupportedException();
	}

	public override void SetLength(
		long value
	) {
		throw new NotSupportedException();
	}

	public override void Write(
		byte[] buffer,
		int offset,
		int count
	) {
		throw new NotSupportedException(
			"Synchronous terminal output is not supported by this adapter."
		);
	}

	public override ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		return this.output.WriteAsync(
			buffer,
			cancellationToken
		);
	}
}
