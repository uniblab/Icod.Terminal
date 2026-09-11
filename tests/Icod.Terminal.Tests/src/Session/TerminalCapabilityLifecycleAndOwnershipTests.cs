/*
	Icod.Terminal.Tests
	Automated test suite for the Icod.Terminal library.
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
namespace Icod.Terminal.Tests.Session;

using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Qualifies C105 lifecycle generation and query-ownership semantics for public capability planning.
/// </summary>
public sealed class TerminalCapabilityLifecycleAndOwnershipTests {
	[Fact]
	public async Task LiveEvidenceExpiresWhenSessionStateIsInvalidated() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			new RecordingTerminalControlProvider(),
			output
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);

		TerminalCapabilityStatus before = session.InspectCapability(
			TerminalCapability.RasterGraphics
		);
		Assert.Equal( TerminalCapabilitySupport.Verified, before.Support );
		Assert.Equal(
			TerminalCapabilityEvidenceKind.LiveObservation,
			before.EvidenceKind
		);

		session.InvalidateState();

		TerminalCapabilityStatus after = session.InspectCapability(
			TerminalCapability.RasterGraphics
		);
		Assert.Equal( TerminalCapabilitySupport.Unknown, after.Support );
		Assert.Equal( TerminalCapabilityEvidenceKind.None, after.EvidenceKind );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task StaticEvidenceSurvivesSessionStateInvalidation() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "capability-static" )
			.SetExtendedString( "Ms", "\u001b]52;%p1%s;%p2%s\u001b\\" )
			.Build();
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			terminal,
			new RecordingTerminalControlProvider(),
			output
		);

		TerminalCapabilityStatus before = session.InspectCapability(
			TerminalCapability.ClipboardWrite
		);
		Assert.Equal( TerminalCapabilitySupport.Advertised, before.Support );
		Assert.Equal(
			TerminalCapabilityEvidenceKind.StaticDescription,
			before.EvidenceKind
		);

		session.InvalidateState();

		TerminalCapabilityStatus after = session.InspectCapability(
			TerminalCapability.ClipboardWrite
		);
		Assert.Equal( TerminalCapabilitySupport.Advertised, after.Support );
		Assert.Equal(
			TerminalCapabilityEvidenceKind.StaticDescription,
			after.EvidenceKind
		);
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task ConcurrentInspectionIsDeterministicAndSideEffectFree() {
		TerminalDescription terminal = new TerminalDescriptionBuilder( "capability-concurrent" )
			.SetExtendedString( "Ms", "\u001b]52;%p1%s;%p2%s\u001b\\" )
			.Build();
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			terminal,
			new RecordingTerminalControlProvider(),
			output
		);

		Task<TerminalCapabilityStatus>[] tasks = Enumerable.Range( 0, 32 )
			.Select(
				_ => Task.Run(
					() => session.InspectCapability( TerminalCapability.ClipboardWrite )
				)
			)
			.ToArray();

		TerminalCapabilityStatus[] statuses = await Task.WhenAll( tasks );
		foreach ( TerminalCapabilityStatus status in statuses ) {
			Assert.Equal( TerminalCapabilitySupport.Advertised, status.Support );
			Assert.Equal(
				TerminalCapabilityEvidenceKind.StaticDescription,
				status.EvidenceKind
			);
			Assert.True( status.IsUsable );
		}
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task PreCancelledVerificationEmitsNoTerminalTraffic() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			new RecordingTerminalControlProvider(),
			output
		);
		using CancellationTokenSource cancellation = new();
		cancellation.Cancel();

		await Assert.ThrowsAsync<OperationCanceledException>(
			async () => await session.VerifyCapabilityAsync(
				TerminalCapability.KeyboardReporting,
				cancellation.Token
			)
		);
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task VerificationRespectsSuspendedQueryOwnership() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			new RecordingTerminalControlProvider(),
			output
		);
		session.SuspendQueryTransactions();
		try {
			await Assert.ThrowsAsync<InvalidOperationException>(
				async () => await session.VerifyCapabilityAsync(
					TerminalCapability.KeyboardReporting
				)
			);
			Assert.Empty( output.Bytes );
		} finally {
			session.ResumeQueryTransactions();
		}
	}

	[Fact]
	public async Task VerificationRespectsClosedQueryOwnership() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			new RecordingTerminalControlProvider(),
			output
		);
		await session.CloseQueryTransactionsAsync();

		await Assert.ThrowsAsync<ObjectDisposedException>(
			async () => await session.VerifyCapabilityAsync(
				TerminalCapability.KeyboardReporting
			)
		);
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task UnavailableEndpointDoesNotTriggerRasterProbeTraffic() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			new RedirectedOutputControlProvider(),
			output
		);

		TerminalCapabilityStatus status = await session.VerifyCapabilityAsync(
			TerminalCapability.RasterGraphics
		);

		Assert.Equal(
			TerminalCapabilityEndpointAvailability.Unavailable,
			status.EndpointAvailability
		);
		Assert.False( status.IsUsable );
		Assert.Empty( output.Bytes );
	}

	[Fact]
	public async Task CapabilityWithoutReviewedProbeRemainsInspectionOnly() {
		RecordingOutput output = new();
		await using TerminalSession session = await OpenSessionAsync(
			TerminalProfiles.Dumb,
			new RecordingTerminalControlProvider(),
			output
		);

		TerminalCapabilityStatus before = session.InspectCapability(
			TerminalCapability.ClipboardWrite
		);
		TerminalCapabilityStatus after = await session.VerifyCapabilityAsync(
			TerminalCapability.ClipboardWrite
		);

		Assert.Equal( before.Support, after.Support );
		Assert.Equal( before.EvidenceKind, after.EvidenceKind );
		Assert.Equal( before.EndpointAvailability, after.EndpointAvailability );
		Assert.Equal( before.IsUsable, after.IsUsable );
		Assert.Empty( output.Bytes );
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		TerminalDescription terminal,
		ITerminalControlProvider controlProvider,
		ITerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( output );

		return TerminalSession.OpenAsync(
			controlProvider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			new EmptyTerminalInput(),
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			}
		);
	}

	private sealed class EmptyTerminalInput : ITerminalInput {
		public ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.FromResult( 0 );
		}
	}

	private sealed class RecordingOutput : ITerminalOutput {
		private readonly object sync = new();
		private readonly List<byte> bytes = [];

		internal IReadOnlyList<byte> Bytes {
			get {
				lock ( this.sync ) {
					return this.bytes.ToArray();
				}
			}
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.sync ) {
				this.bytes.AddRange( buffer.ToArray() );
			}
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
	}

	private class RecordingTerminalControlProvider : ITerminalControlProvider {
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

		public virtual TerminalControlResult<TerminalEndpointObservation> Observe(
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
			return TerminalControlResult<TerminalSize>.Unsupported(
				"Size is not required by this test."
			);
		}

		public TerminalControlResult<TerminalModeSnapshot> GetMode(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			return TerminalControlResult<TerminalModeSnapshot>.Available( this.baseline );
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

	private sealed class RedirectedOutputControlProvider : RecordingTerminalControlProvider {
		public override TerminalControlResult<TerminalEndpointObservation> Observe(
			TerminalEndpoint endpoint
		) {
			ArgumentNullException.ThrowIfNull( endpoint );
			bool isInput = ReferenceEquals( endpoint, TerminalEndpoint.StandardInput );
			return TerminalControlResult<TerminalEndpointObservation>.Available(
				new TerminalEndpointObservation(
					isInput,
					null,
					isInput ? TerminalPlatformKind.PosixTermios : null,
					isInput
						? TerminalControlCapabilities.Attachment
							| TerminalControlCapabilities.ModeRead
							| TerminalControlCapabilities.ModeWrite
						: TerminalControlCapabilities.None
				)
			);
		}
	}
}
