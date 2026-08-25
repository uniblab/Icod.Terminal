namespace Icod.Terminal.Tests.Control;

using Icod.Terminal;
using Xunit;

/// <summary>
/// Verifies stable terminal-mode serialization, restoration, and validation
/// without requiring a live terminal.
/// </summary>
public sealed class TerminalModeCodecTests {
	/// <summary>
	/// Verifies that the POSIX form remains the established colon-separated
	/// hexadecimal flags and complete native control-character array.
	/// </summary>
	[Fact]
	public void SerializesAndRestoresPosixState() {
		TerminalModeSnapshot baseline = CreatePosixMode(
			0x11,
			0x22,
			0x33,
			0x44,
			new byte[] {
				0,
				1,
				0x7f,
				0xff
			}
		);
		string serialized = TerminalModeCodec.Serialize( baseline );

		Assert.Equal(
			"11:22:33:44:0:1:7f:ff",
			serialized
		);

		bool succeeded = TerminalModeCodec.TryRestore(
			"1:2:3:4:a:b:c:d",
			baseline,
			out TerminalModeSnapshot? restored,
			out string? error
		);

		Assert.True( succeeded );
		Assert.Null( error );
		Assert.NotNull( restored );

		TerminalModeSnapshot actual = restored!;

		Assert.Equal(
			1UL,
			actual.InputFlags
		);
		Assert.Equal(
			2UL,
			actual.OutputFlags
		);
		Assert.Equal(
			3UL,
			actual.ControlFlags
		);
		Assert.Equal(
			4UL,
			actual.LocalFlags
		);
		Assert.Equal(
			new byte[] {
				0x0a,
				0x0b,
				0x0c,
				0x0d
			},
			actual.ControlCharacters
		);
		Assert.Equal(
			baseline.InputSpeed,
			actual.InputSpeed
		);
		Assert.Equal(
			baseline.OutputSpeed,
			actual.OutputSpeed
		);
		Assert.Equal(
			baseline.LineDiscipline,
			actual.LineDiscipline
		);
	}

	/// <summary>
	/// Verifies that POSIX restoration requires the host's exact control-array
	/// length and byte-sized hexadecimal values.
	/// </summary>
	[Theory]
	[InlineData( "1:2:3:4:5" )]
	[InlineData( "1:2:3:4:0:1:2:100" )]
	[InlineData( "1:2:3:4:0:1:2:not-hex" )]
	public void RejectsMalformedPosixState(
		string serialized
	) {
		bool succeeded = TerminalModeCodec.TryRestore(
			serialized,
			CreatePosixMode(
				0,
				0,
				0,
				0,
				new byte[] {
					0,
					0,
					0,
					0
				}
			),
			out TerminalModeSnapshot? restored,
			out string? error
		);

		Assert.False( succeeded );
		Assert.Null( restored );
		Assert.False(
			string.IsNullOrWhiteSpace( error )
		);
	}

	/// <summary>
	/// Verifies that a serialized flag cannot exceed the destination ABI's
	/// native 32-bit flag width.
	/// </summary>
	[Fact]
	public void RejectsFlagWiderThanDestinationAbi() {
		bool succeeded = TerminalModeCodec.TryRestore(
			"100000000:2:3:4:0:0:0:0",
			CreatePosixMode(
				0,
				0,
				0,
				0,
				new byte[] {
					0,
					0,
					0,
					0
				}
			),
			out TerminalModeSnapshot? restored,
			out string? error
		);

		Assert.False( succeeded );
		Assert.Null( restored );
		Assert.Contains(
			"32-bit",
			error!
		);
	}

	/// <summary>
	/// Verifies stable Windows input and output serialization and restoration.
	/// </summary>
	[Theory]
	[InlineData(
		TerminalConsoleDirection.Input,
		"win32-v1-input:00001234"
	)]
	[InlineData(
		TerminalConsoleDirection.Output,
		"win32-v1-output:00001234"
	)]
	public void SerializesAndRestoresWindowsConsoleMode(
		TerminalConsoleDirection direction,
		string expected
	) {
		TerminalModeSnapshot baseline =
			TerminalModeSnapshot.CreateWindowsConsole(
				direction,
				0x1234
			);

		Assert.Equal(
			expected,
			TerminalModeCodec.Serialize( baseline )
		);
		Assert.True(
			TerminalModeCodec.TryRestore(
				expected,
				baseline,
				out TerminalModeSnapshot? restored,
				out string? error
			)
		);
		Assert.Null( error );
		Assert.NotNull( restored );

		TerminalModeSnapshot actual = restored!;

		Assert.Equal(
			direction,
			actual.ConsoleDirection!.Value
		);
		Assert.Equal(
			0x1234U,
			actual.ConsoleMode!.Value
		);
	}

	/// <summary>Verifies that the versioned Windows form requires eight hexadecimal digits.</summary>
	[Theory]
	[InlineData( "win32-v1-input:1" )]
	[InlineData( "win32-v1-input:000000001" )]
	[InlineData( "win32-v1-input:not-hex!" )]
	public void RejectsMalformedWindowsConsoleMode(
		string serialized
	) {
		Assert.False(
			TerminalModeCodec.TryRestore(
				serialized,
				TerminalModeSnapshot.CreateWindowsConsole(
					TerminalConsoleDirection.Input,
					0
				),
				out TerminalModeSnapshot? restored,
				out string? error
			)
		);
		Assert.Null( restored );
		Assert.False(
			string.IsNullOrWhiteSpace( error )
		);
	}

	/// <summary>
	/// Verifies that a console-input mode cannot be restored against an output
	/// handle baseline, or vice versa.
	/// </summary>
	[Fact]
	public void RejectsWindowsDirectionMismatch() {
		bool succeeded = TerminalModeCodec.TryRestore(
			"win32-v1-input:00000001",
			TerminalModeSnapshot.CreateWindowsConsole(
				TerminalConsoleDirection.Output,
				0
			),
			out TerminalModeSnapshot? restored,
			out string? error
		);

		Assert.False( succeeded );
		Assert.Null( restored );
		Assert.Contains(
			"output",
			error!
		);
	}

	private static TerminalModeSnapshot CreatePosixMode(
		ulong inputFlags,
		ulong outputFlags,
		ulong controlFlags,
		ulong localFlags,
		IEnumerable<byte> characters
	) {
		ArgumentNullException.ThrowIfNull( characters );

		return TerminalModeSnapshot.CreatePosix(
			inputFlags,
			outputFlags,
			controlFlags,
			localFlags,
			characters,
			0,
			32,
			7,
			new TerminalSpeed(
				13,
				9600
			),
			new TerminalSpeed(
				14,
				19200
			)
		);
	}
}