/*
	Icod.Terminal.TermInfoIntegration.Tests
	Validation utility for Icod.Terminal release and integration contracts.
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
namespace Icod.Terminal.TermInfoIntegration.Tests;

using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Icod.TermInfo;
using Icod.TermInfo.Inspection;
using Xunit;

/// <summary>
/// Proves the loose-coupling contract from static TermInfo planning through Terminal-owned live
/// verification and caller-owned evidence strengthening.
/// </summary>
public sealed class PersistentRasterLifecycleIntegrationTests {
	[Fact]
	public async Task LiveVerificationStrengthensIndeterminatePlanToSuccess() {
		TerminalDescription description = CreateStaticallyIndeterminateDescription();
		PersistentRasterLifecycleRequest request = CreatePersistentRequest();
		PersistentRasterLifecycleProfile staticProfile =
			PersistentRasterLifecycleInspector.Inspect( description );
		PersistentRasterLifecyclePlan staticPlan =
			PersistentRasterLifecyclePlanner.Plan( staticProfile, request );
		ScriptedTerminalTransport transport = new();

		Assert.Equal( PersistentRasterLifecyclePlanStatus.Indeterminate, staticPlan.Status );
		Assert.True( staticPlan.RequiresRuntimeVerification );
		Assert.Empty( transport.Writes );

		await using TerminalSession session = await OpenSessionAsync(
			description,
			new RecordingTerminalControlProvider(),
			transport
		);
		Task<TerminalCapabilityStatus> verification = session.VerifyCapabilityAsync(
			TerminalCapability.PersistentRasterGraphics
		).AsTask();
		string requestText = await transport.WaitForWriteAsync();
		uint imageId = ExtractKittyProbeImageId( requestText );
		transport.Publish(
			$"\u001b_Gi={imageId.ToString( CultureInfo.InvariantCulture )};OK\u001b\\"
		);
		transport.Publish( "\u001b[?64;1c" );

		TerminalCapabilityStatus status = await verification;
		Assert.Equal( TerminalCapabilitySupport.Verified, status.Support );
		Assert.Equal( TerminalCapabilityEvidenceKind.LiveObservation, status.EvidenceKind );
		Assert.True( status.IsUsable );

		PersistentRasterLifecyclePlan strengthenedPlan = Replan(
			staticProfile,
			request,
			status
		);

		Assert.Equal( PersistentRasterLifecyclePlanStatus.Success, strengthenedPlan.Status );
		Assert.False( strengthenedPlan.RequiresRuntimeVerification );
	}

	[Fact]
	public async Task VerifiedNonSupportStrengthensIndeterminatePlanToImpossible() {
		TerminalDescription description = CreateStaticallyIndeterminateDescription();
		PersistentRasterLifecycleRequest request = CreatePersistentRequest();
		PersistentRasterLifecycleProfile staticProfile =
			PersistentRasterLifecycleInspector.Inspect( description );
		PersistentRasterLifecyclePlan staticPlan =
			PersistentRasterLifecyclePlanner.Plan( staticProfile, request );
		ScriptedTerminalTransport transport = new();

		Assert.Equal( PersistentRasterLifecyclePlanStatus.Indeterminate, staticPlan.Status );
		Assert.True( staticPlan.RequiresRuntimeVerification );
		Assert.Empty( transport.Writes );

		await using TerminalSession session = await OpenSessionAsync(
			description,
			new RecordingTerminalControlProvider(),
			transport
		);
		Task<TerminalCapabilityStatus> verification = session.VerifyCapabilityAsync(
			TerminalCapability.PersistentRasterGraphics
		).AsTask();
		_ = await transport.WaitForWriteAsync();
		transport.Publish( "\u001b[?64;1c" );

		TerminalCapabilityStatus status = await verification;
		Assert.Equal( TerminalCapabilitySupport.Unsupported, status.Support );
		Assert.Equal( TerminalCapabilityEvidenceKind.LiveObservation, status.EvidenceKind );
		Assert.False( status.IsUsable );

		PersistentRasterLifecyclePlan strengthenedPlan = Replan(
			staticProfile,
			request,
			status
		);

		Assert.Equal( PersistentRasterLifecyclePlanStatus.Impossible, strengthenedPlan.Status );
		Assert.False( strengthenedPlan.RequiresRuntimeVerification );
	}

	[Fact]
	public async Task StaticLifecycleDeclarationsPlanWithoutProbeButDoNotMakeRedirectedSessionUsable() {
		TerminalDescription description = CreateStaticallySupportedDescription();
		PersistentRasterLifecycleRequest request = CreatePersistentRequest();
		PersistentRasterLifecycleProfile profile =
			PersistentRasterLifecycleInspector.Inspect( description );
		PersistentRasterLifecyclePlan plan =
			PersistentRasterLifecyclePlanner.Plan( profile, request );
		RecordingOutput output = new();

		Assert.Equal( PersistentRasterLifecyclePlanStatus.Success, plan.Status );
		Assert.False( plan.RequiresRuntimeVerification );
		Assert.Empty( output.Bytes );

		await using TerminalSession session = await OpenSessionAsync(
			description,
			new RedirectedOutputControlProvider(),
			new EmptyTerminalInput(),
			output
		);
		TerminalCapabilityStatus runtimeStatus = session.InspectCapability(
			TerminalCapability.PersistentRasterGraphics
		);

		Assert.Equal(
			TerminalCapabilityEndpointAvailability.Unavailable,
			runtimeStatus.EndpointAvailability
		);
		Assert.False( runtimeStatus.IsUsable );
		Assert.Empty( output.Bytes );
	}

	private static PersistentRasterLifecycleRequest CreatePersistentRequest() {
		return new PersistentRasterLifecycleRequest(
			uploadResource: true,
			placementCount: 1,
			updatePlacement: true,
			deletePlacement: true,
			deleteResource: true,
			requireAcknowledgedUpload: true
		);
	}

	private static TerminalDescription CreateStaticallyIndeterminateDescription() {
		return new TerminalDescriptionBuilder( "terminal-integration-indeterminate" )
			.SetExtendedBoolean( "Sixel" )
			.Build();
	}

	private static TerminalDescription CreateStaticallySupportedDescription() {
		return new TerminalDescriptionBuilder( "terminal-integration-supported" )
			.SetExtendedBoolean(
				PersistentRasterLifecycleInspector.PersistentUploadCapabilityName
			)
			.SetExtendedBoolean(
				PersistentRasterLifecycleInspector.AcknowledgedUploadCapabilityName
			)
			.SetExtendedBoolean(
				PersistentRasterLifecycleInspector.PlacementCreationCapabilityName
			)
			.SetExtendedBoolean(
				PersistentRasterLifecycleInspector.MultiplePlacementsCapabilityName
			)
			.SetExtendedBoolean(
				PersistentRasterLifecycleInspector.PlacementUpdateCapabilityName
			)
			.SetExtendedBoolean(
				PersistentRasterLifecycleInspector.PlacementDeletionCapabilityName
			)
			.SetExtendedBoolean(
				PersistentRasterLifecycleInspector.ResourceDeletionCapabilityName
			)
			.Build();
	}

	private static PersistentRasterLifecyclePlan Replan(
		PersistentRasterLifecycleProfile staticProfile,
		PersistentRasterLifecycleRequest request,
		TerminalCapabilityStatus status
	) {
		ArgumentNullException.ThrowIfNull( staticProfile );
		ArgumentNullException.ThrowIfNull( request );

		List<PersistentRasterLifecycleEvidence> strengthenedEvidence =
			staticProfile.Evidence.ToList();
		strengthenedEvidence.AddRange(
			PersistentRasterLifecycleEvidenceBridge.CreateEvidence(
				status,
				GetNextSourceOrdinal( staticProfile.Evidence )
			)
		);
		PersistentRasterLifecycleProfile strengthenedProfile =
			PersistentRasterLifecycleClassifier.Classify( strengthenedEvidence );
		return PersistentRasterLifecyclePlanner.Plan( strengthenedProfile, request );
	}

	private static int GetNextSourceOrdinal(
		IReadOnlyList<PersistentRasterLifecycleEvidence> evidence
	) {
		ArgumentNullException.ThrowIfNull( evidence );
		return 0 == evidence.Count
			? 0
			: checked( evidence.Max( item => item.SourceOrdinal ) + 1 );
	}

	private static uint ExtractKittyProbeImageId(
		string request
	) {
		ArgumentException.ThrowIfNullOrWhiteSpace( request );
		const string prefix = "\u001b_Gi=";
		int marker = request.IndexOf( prefix, StringComparison.Ordinal );
		if ( 0 > marker ) {
			throw new InvalidOperationException(
				"The Terminal persistent-raster verification did not emit its expected support query."
			);
		}
		int valueStart = marker + prefix.Length;
		int valueEnd = request.IndexOf( ',', valueStart );
		if ( valueStart >= valueEnd ) {
			throw new InvalidOperationException(
				"The Terminal persistent-raster support query did not contain a parseable image identity."
			);
		}
		return uint.Parse(
			request.AsSpan( valueStart, valueEnd - valueStart ),
			NumberStyles.None,
			CultureInfo.InvariantCulture
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		TerminalDescription terminal,
		ITerminalControlProvider controlProvider,
		ScriptedTerminalTransport transport
	) {
		ArgumentNullException.ThrowIfNull( transport );
		return OpenSessionAsync(
			terminal,
			controlProvider,
			transport,
			transport
		);
	}

	private static ValueTask<TerminalSession> OpenSessionAsync(
		TerminalDescription terminal,
		ITerminalControlProvider controlProvider,
		ITerminalInput input,
		ITerminalOutput output
	) {
		ArgumentNullException.ThrowIfNull( terminal );
		ArgumentNullException.ThrowIfNull( controlProvider );
		ArgumentNullException.ThrowIfNull( input );
		ArgumentNullException.ThrowIfNull( output );
		return TerminalSession.OpenAsync(
			controlProvider,
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			input,
			output,
			new TerminalSessionOptions {
				TerminalOverride = terminal,
				ConfigureOutput = false,
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			}
		);
	}

	private sealed class ScriptedTerminalTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte> input = Channel.CreateUnbounded<byte>();
		private readonly object sync = new();
		private readonly List<string> writes = [];
		private readonly TaskCompletionSource<string> firstWrite = new(
			TaskCreationOptions.RunContinuationsAsynchronously
		);

		internal IReadOnlyList<string> Writes {
			get {
				lock ( this.sync ) {
					return this.writes.ToArray();
				}
		}
		}

		internal Task<string> WaitForWriteAsync() {
			return this.firstWrite.Task;
		}

		internal void Publish(
			string value
		) {
			ArgumentNullException.ThrowIfNull( value );
			foreach ( byte item in Encoding.ASCII.GetBytes( value ) ) {
				if ( !this.input.Writer.TryWrite( item ) ) {
					throw new InvalidOperationException(
						"The scripted Terminal input channel rejected a response byte."
					);
				}
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			if ( buffer.IsEmpty ) {
				return 0;
			}
			byte first = await this.input.Reader.ReadAsync( cancellationToken );
			buffer.Span[ 0 ] = first;
			int count = 1;
			while ( count < buffer.Length && this.input.Reader.TryRead( out byte item ) ) {
				buffer.Span[ count ] = item;
				++count;
			}
			return count;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			string value = Encoding.ASCII.GetString( buffer.Span );
			lock ( this.sync ) {
				this.writes.Add( value );
			}
			this.firstWrite.TrySetResult( value );
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			return ValueTask.CompletedTask;
		}
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
				"Size is not required by this integration test."
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
