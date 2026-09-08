/*
	Icod.Terminal.PackageHardeningSmoke
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
using Icod.Terminal;
using Icod.TermInfo;

const string EnablePaste = "<P+>";
const string DisablePaste = "<P->";
const string EnterAlternateScreen = "<A+>";
const string ExitAlternateScreen = "<A->";

RecordingTransport transport = new();
TerminalSession session = await OpenSessionAsync( transport );

TerminalInputProtocolLease inputLease = (
	await session.AcquireInputProtocolsAsync(
		new TerminalInputProtocolOptions {
			BracketedPaste = true
		}
	)
).GetRequiredValue();
TerminalPresentationLease presentationLease = (
	await session.AcquirePresentationAsync(
		new TerminalPresentationOptions {
			AlternateScreen = true
		}
	)
).GetRequiredValue();

RequireSequence(
	transport.Writes,
	EnablePaste,
	EnterAlternateScreen
);

await presentationLease.DisposeAsync();
await inputLease.DisposeAsync();
RequireSequence(
	transport.Writes,
	EnablePaste,
	EnterAlternateScreen,
	ExitAlternateScreen,
	DisablePaste
);

transport.Writes.Clear();
Task disposal = session.DisposeAsync().AsTask();

await RequireThrowsAsync<ObjectDisposedException>(
	() => session.AcquireInputProtocolsAsync(
		new TerminalInputProtocolOptions {
			BracketedPaste = true
		}
	).AsTask(),
	"Rich-input acquisition was not rejected after package-session teardown began."
);
await RequireThrowsAsync<ObjectDisposedException>(
	() => session.AcquirePresentationAsync(
		new TerminalPresentationOptions {
			AlternateScreen = true
		}
	).AsTask(),
	"Presentation acquisition was not rejected after package-session teardown began."
);

await disposal;
Require(
	0 == transport.Writes.Count,
	"Rejected post-teardown state acquisition emitted terminal-control output."
);

Console.WriteLine(
	"Icod.Terminal 0.18 package teardown/state-acquisition hardening smoke passed."
);

static async Task RequireThrowsAsync<TException>(
	Func<Task> action,
	string message
) where TException : Exception {
	ArgumentNullException.ThrowIfNull( action );
	ArgumentNullException.ThrowIfNull( message );
	try {
		await action().ConfigureAwait( false );
	} catch ( TException ) {
		return;
	}
	throw new InvalidOperationException( message );
}

static void Require(
	bool condition,
	string message
) {
	ArgumentNullException.ThrowIfNull( message );
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}

static void RequireSequence(
	IReadOnlyList<string> actual,
	params string[] expected
) {
	ArgumentNullException.ThrowIfNull( actual );
	ArgumentNullException.ThrowIfNull( expected );
	Require(
		actual.SequenceEqual( expected, StringComparer.Ordinal ),
		"Unexpected terminal-control sequence: "
			+ string.Join( " | ", actual )
	);
}

static ValueTask<TerminalSession> OpenSessionAsync(
	RecordingTransport transport
) {
	ArgumentNullException.ThrowIfNull( transport );

	TerminalDescription terminal = new TerminalDescriptionBuilder(
		"package-hardening-smoke"
	)
		.SetString(
			StringCapability.EnterCursorAddressingMode,
			EnterAlternateScreen
		)
		.SetString(
			StringCapability.ExitCursorAddressingMode,
			ExitAlternateScreen
		)
		.SetExtendedString( "BE", EnablePaste )
		.SetExtendedString( "BD", DisablePaste )
		.SetExtendedString( "PS", "\u001b[200~" )
		.SetExtendedString( "PE", "\u001b[201~" )
		.Build();

	return TerminalSession.OpenAsync(
		new TestTerminalControlProvider(),
		TerminalEndpoint.StandardInput,
		TerminalEndpoint.StandardOutput,
		transport,
		transport,
		new TerminalSessionOptions {
			TerminalOverride = terminal,
			ConfigureOutput = false,
			ObserveLifecycleEvents = false
		}
	);
}

internal sealed class RecordingTransport : ITerminalInput, ITerminalOutput {
	internal List<string> Writes {
		get;
	} = [];

	public ValueTask<int> ReadAsync(
		Memory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.FromResult( 0 );
	}

	public ValueTask WriteAsync(
		ReadOnlyMemory<byte> buffer,
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		this.Writes.Add( Encoding.Latin1.GetString( buffer.Span ) );
		return ValueTask.CompletedTask;
	}

	public ValueTask FlushAsync(
		CancellationToken cancellationToken = default
	) {
		cancellationToken.ThrowIfCancellationRequested();
		return ValueTask.CompletedTask;
	}
}

internal sealed class TestTerminalControlProvider : ITerminalControlProvider {
	private readonly TerminalModeSnapshot baseline = TerminalModeSnapshot.CreatePosix(
		0,
		0,
		0,
		0x0002UL,
		new byte[ 32 ],
		0,
		32,
		0,
		new TerminalSpeed( 13, 9600 ),
		new TerminalSpeed( 13, 9600 )
	);

	public TerminalControlResult<TerminalEndpointObservation> Observe(
		TerminalEndpoint endpoint
	) {
		ArgumentNullException.ThrowIfNull( endpoint );
		return TerminalControlResult<TerminalEndpointObservation>.Available(
			new TerminalEndpointObservation(
				true,
				null,
				TerminalPlatformKind.PosixTermios,
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
		return TerminalControlResult<TerminalSize>.Unavailable(
			"Live size is not required by package hardening smoke."
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
