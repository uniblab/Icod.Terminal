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
/// Identifies the framing family expected for one terminal response.
/// </summary>
internal enum TerminalResponseFrameKind {
	Csi,
	Dcs,
	Osc,
	Apc,
	Pm,
	Sos
}

/// <summary>
/// Maps response-frame compatibility kinds to the normalized control-family vocabulary.
/// </summary>
internal static class TerminalResponseFrameKinds {
	internal static TerminalControlFamily GetControlFamily(
		TerminalResponseFrameKind kind
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( kind ),
				kind,
				"The terminal response frame kind is not recognized."
			);
		}

		return kind switch {
			TerminalResponseFrameKind.Csi => TerminalControlFamily.Csi,
			TerminalResponseFrameKind.Dcs => TerminalControlFamily.Dcs,
			TerminalResponseFrameKind.Osc => TerminalControlFamily.Osc,
			TerminalResponseFrameKind.Apc => TerminalControlFamily.Apc,
			TerminalResponseFrameKind.Pm => TerminalControlFamily.Pm,
			TerminalResponseFrameKind.Sos => TerminalControlFamily.Sos,
			_ => throw new InvalidOperationException(
				"The terminal response frame kind is not recognized."
			)
		};
	}

	internal static TerminalResponseFrameKind GetFrameKind(
		TerminalControlFamily family
	) {
		if ( !Enum.IsDefined( family ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( family ),
				family,
				"The terminal control family is not recognized."
			);
		}

		return family switch {
			TerminalControlFamily.Csi => TerminalResponseFrameKind.Csi,
			TerminalControlFamily.Dcs => TerminalResponseFrameKind.Dcs,
			TerminalControlFamily.Osc => TerminalResponseFrameKind.Osc,
			TerminalControlFamily.Apc => TerminalResponseFrameKind.Apc,
			TerminalControlFamily.Pm => TerminalResponseFrameKind.Pm,
			TerminalControlFamily.Sos => TerminalResponseFrameKind.Sos,
			_ => throw new InvalidOperationException(
				"The terminal control family is not recognized."
			)
		};
	}
}

/// <summary>
/// Matches one completely framed terminal response.
/// </summary>
internal interface ITerminalResponseMatcher {
	TerminalResponseFrameKind FrameKind {
		get;
	}

	bool IsMatch(
		TerminalResponseFrame frame
	);
}

/// <summary>
/// Identifies a response matcher which can claim a structurally correlated
/// response prefix before complete semantic parsing is possible.
/// </summary>
internal interface ICorrelatedTerminalResponseMatcher {
	bool IsCorrelatedPrefix(
		IReadOnlyList<byte> bytes
	);
}

/// <summary>
/// Represents one complete response frame retained as exact terminal bytes.
/// </summary>
internal sealed class TerminalResponseFrame {
	private readonly byte[] bytes;

	internal TerminalResponseFrame(
		TerminalResponseFrameKind kind,
		byte[] bytes
	) {
		if ( !Enum.IsDefined( kind ) ) {
			throw new ArgumentOutOfRangeException(
				nameof( kind ),
				kind,
				"The terminal response frame kind is not recognized."
			);
		}
		ArgumentNullException.ThrowIfNull( bytes );
		if ( 0 == bytes.Length ) {
			throw new ArgumentException(
				"A terminal response frame cannot be empty.",
				nameof( bytes )
			);
		}

		this.Kind = kind;
		this.bytes = bytes.ToArray();
	}

	internal TerminalResponseFrameKind Kind {
		get;
	}

	internal ReadOnlyMemory<byte> Bytes {
		get {
			return this.bytes;
		}
	}

	internal bool UsesEightBitIntroducer {
		get {
			return this.Kind switch {
				TerminalResponseFrameKind.Csi => 0x9B == this.bytes[ 0 ],
				TerminalResponseFrameKind.Dcs => 0x90 == this.bytes[ 0 ],
				TerminalResponseFrameKind.Osc => 0x9D == this.bytes[ 0 ],
				TerminalResponseFrameKind.Apc => 0x9F == this.bytes[ 0 ],
				TerminalResponseFrameKind.Pm => 0x9E == this.bytes[ 0 ],
				TerminalResponseFrameKind.Sos => 0x98 == this.bytes[ 0 ],
				_ => throw new InvalidOperationException(
					"The terminal response frame kind is not recognized."
				)
			};
		}
	}
}

/// <summary>
/// Owns one internal response expectation without exposing routing state publicly.
/// </summary>
internal sealed class TerminalResponseExpectation {
	private readonly TaskCompletionSource<TerminalResponseFrame> completion = new(
		TaskCreationOptions.RunContinuationsAsynchronously
	);

	private int protectedBufferedBytes;
	private int responseDisposition = (int)TerminalQueryResponseDisposition.Completion;
	private int armed;

	internal TerminalResponseExpectation(
		ITerminalResponseMatcher matcher
	) : this( TerminalQueryResponsePlan.ForCompletion( matcher ) ) {
	}

	internal TerminalResponseExpectation(
		TerminalQueryResponsePlan responsePlan
	) {
		ArgumentNullException.ThrowIfNull( responsePlan );
		this.ResponsePlan = responsePlan;
	}

	internal TerminalQueryResponsePlan ResponsePlan {
		get;
	}

	internal Task<TerminalResponseFrame> Response {
		get {
			return this.completion.Task;
		}
	}

	internal TerminalQueryResponseDisposition ResponseDisposition {
		get {
			return (TerminalQueryResponseDisposition)Volatile.Read(
				ref this.responseDisposition
			);
		}
	}

	internal bool IsArmed {
		get {
			return 0 != Volatile.Read( ref this.armed );
		}
	}

	internal int ProtectedBufferedBytes {
		get {
			return Volatile.Read( ref this.protectedBufferedBytes );
		}
	}

	internal void Arm(
		int protectedBufferedBytes
	) {
		if ( 0 > protectedBufferedBytes ) {
			throw new ArgumentOutOfRangeException( nameof( protectedBufferedBytes ) );
		}
		if ( this.IsArmed ) {
			throw new InvalidOperationException(
				"The terminal response expectation is already armed."
			);
		}

		Volatile.Write(
			ref this.protectedBufferedBytes,
			protectedBufferedBytes
		);
		Volatile.Write( ref this.armed, 1 );
	}

	internal void ConsumeProtectedBytes(
		int count
	) {
		if ( 0 > count ) {
			throw new ArgumentOutOfRangeException( nameof( count ) );
		}
		int protectedBytes = Volatile.Read( ref this.protectedBufferedBytes );
		if ( 0 == count || 0 == protectedBytes ) {
			return;
		}

		Volatile.Write(
			ref this.protectedBufferedBytes,
			Math.Max(
				0,
				protectedBytes - count
			)
		);
	}

	internal bool TrySetResult(
		TerminalResponseFrame frame
	) {
		return this.TrySetResult(
			frame,
			TerminalQueryResponseDisposition.Completion
		);
	}

	internal bool TrySetResult(
		TerminalResponseFrame frame,
		TerminalQueryResponseDisposition disposition
	) {
		ArgumentNullException.ThrowIfNull( frame );
		if ( !Enum.IsDefined( disposition ) ) {
			throw new ArgumentOutOfRangeException( nameof( disposition ) );
		}

		Volatile.Write(
			ref this.responseDisposition,
			(int)disposition
		);
		return this.completion.TrySetResult( frame );
	}

	internal bool TrySetException(
		Exception exception
	) {
		ArgumentNullException.ThrowIfNull( exception );
		return this.completion.TrySetException( exception );
	}

	internal bool TrySetCanceled() {
		return this.completion.TrySetCanceled();
	}
}
