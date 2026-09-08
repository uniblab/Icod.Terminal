/*
	Icod.Terminal.PackageCursorStyleSmoke
	Package smoke-test utility for Icod.Terminal release and compatibility contracts.
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
using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;

static void Require(
	bool condition,
	string message
) {
	ArgumentNullException.ThrowIfNull( message );
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

var transport = new ScriptedTransport();
var provider = new PackageTerminalControlProvider();
await using TerminalSession session = await TerminalSession.OpenAsync(
	provider,
	TerminalEndpoint.StandardInput,
	TerminalEndpoint.StandardOutput,
	transport,
	transport,
	new TerminalSessionOptions {
		TerminalOverride = TerminalProfiles.Dumb,
		ConfigureOutput = false,
		ObserveLifecycleEvents = false
	}
);

await session.SetCursorStyleAsync(
	TerminalCursorStyle.SteadyUnderline
);
Require(
	transport.ContainsWrite( "\u001b[4 q" ),
	"The package cursor-style setter did not emit DECSCUSR parameter 4."
);

TerminalCursorStyleObservation observation = await session.QueryCursorStyleAsync(
	TimeSpan.FromSeconds( 1 )
);
Require(
	observation.IsSupported
		&& TerminalCursorStyle.BlinkingUnderline == observation.Style,
	"The package cursor-style query did not return the scripted blinking underline state."
);

await using ( TerminalCursorStyleLease lease = await session.AcquireCursorStyleAsync(
	TerminalCursorStyle.SteadyBar,
	TimeSpan.FromSeconds( 1 )
) ) {
	Require(
		TerminalCursorStyle.SteadyBar == lease.Style,
		"The package cursor-style lease did not retain its semantic style."
	);
	Require(
		transport.ContainsWrite( "\u001b[6 q" ),
		"The package cursor-style lease did not emit DECSCUSR parameter 6."
	);
}

Require(
	transport.ContainsWrite( "\u001b[3 q" ),
	"The package cursor-style lease did not restore the observed baseline."
);

Console.WriteLine( "Icod.Terminal cursor-style package smoke test passed." );

internal sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
	private static readonly byte[] CursorStyleQuery =
		Encoding.ASCII.GetBytes( "\u001bP$q q\u001b\\" );
	private static readonly byte[] CursorStyleResponse =
		Encoding.ASCII.GetBytes( "\u001bP1$r3 q\u001b\\" );

	private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>();
	private readonly List<byte[]> writes = [];

	public async ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		byte[] value = await this.input.Reader.ReadAsync(
			cancellationToken
		).ConfigureAwait( false );
		value.AsSpan().CopyTo( buffer.Span );
		return value.Length;
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		byte[] value = buffer.ToArray();
		this.writes.Add( value );
		if ( value.AsSpan().SequenceEqual( CursorStyleQuery ) ) {
			this.input.Writer.TryWrite( CursorStyleResponse.ToArray() );
		}
		return ValueTask.CompletedTask;
	}

	public ValueTask FlushAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}

	internal bool ContainsWrite(
		string text
	) {
		ArgumentNullException.ThrowIfNull( text );
		byte[] expected = Encoding.ASCII.GetBytes( text );
		return this.writes.Any(
			value => value.AsSpan().SequenceEqual( expected )
		);
	}
}

internal sealed class PackageTerminalControlProvider : ITerminalControlProvider {
	private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreateWindowsConsole(
		TerminalConsoleDirection.Input,
		0x00000007u
	);

	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalEndpointObservation>.Available(
			new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.WindowsConsole,
				TerminalControlCapabilities.Attachment
					| TerminalControlCapabilities.ModeRead
					| TerminalControlCapabilities.ModeWrite
			)
		);
	}

	public TerminalControlResult<TerminalSize> GetSize(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalSize>.Unsupported(
			"Size is not required by the cursor-style package smoke."
		);
	}

	public TerminalControlResult<TerminalModeSnapshot> GetMode(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalModeSnapshot>.Available(
			this.baseline
		);
	}

	public TerminalControlMutationResult SetMode(
		TerminalEndpoint endpoint,
		TerminalModeSnapshot mode,
		TerminalModeApplyTiming timing
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		ArgumentNullException.ThrowIfNull( mode );
		if ( !Enum.IsDefined( timing ) ) {
			throw new ArgumentOutOfRangeException( nameof( timing ) );
		}
		return TerminalControlMutationResult.Success();
	}
}
