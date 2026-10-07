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
namespace Icod.Terminal.Tests.Graphics;

using System.Text;
using System.Threading.Channels;
using Icod.Terminal;
using Icod.TermInfo;
using Xunit;

/// <summary>
/// Freezes T164 per-frame timing and explicit current-frame selection behavior.
/// </summary>
public sealed class TerminalRasterAnimationControlTests {
	[Fact]
	public async Task PartialFrameReplacementUsesAcknowledgedBoundedFrameEdit() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u, width: 2, height: 2
		);
		TerminalRasterImage region = TerminalRasterImage.CreateRgb24(
			1, 1, [ 1, 2, 3 ]
		);

		Task<TerminalControlMutationResult> update = resource.Animation
			.UpdateFrameRegionAsync(
				resource.Animation.RootFrame,
				region,
				1,
				0
			).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=f,f=24,s=1,v=1,t=d,i=77,r=1,x=1,y=0,X=1,m=0;AQID\u001b\\"
			),
			transport.Writes[ 1 ]
		);
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		TerminalControlMutationResult result = await update;
		Assert.True( result.Succeeded );
		Assert.Equal(
			TerminalControlMutationConfirmation.ProtocolAcknowledged,
			result.Confirmation
		);
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
		Assert.Equal(
			TerminalCapabilitySupport.Verified,
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameRegionUpdateRgb24
			).Support
		);
		Assert.Equal(
			TerminalCapabilitySupport.Unknown,
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameRegionUpdateRgba32
			).Support
		);
	}

	[Fact]
	public async Task PartialFrameReplacementRejectsInvalidInputsBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource first = await CreateResourceAsync(
			session, transport, imageId: 77u, width: 2, height: 2
		);
		await using TerminalRasterResource second = await CreateResourceAsync(
			session, transport, imageId: 88u, expectedWriteCount: 2,
			width: 2, height: 2
		);
		TerminalRasterImage pixel = TerminalRasterImage.CreateRgba32(
			1, 1, [ 1, 2, 3, 4 ]
		);
		TerminalRasterImage indexed = TerminalRasterImage.CreateIndexed8(
			1, 1, [ 0 ], [ new TerminalRasterColor( 1, 2, 3 ) ]
		);
		using CancellationTokenSource cancelled = new();
		cancelled.Cancel();
		int writesBefore = transport.Writes.Count;

		Assert.Throws<ArgumentNullException>(
			() => first.Animation.UpdateFrameRegionAsync(
				first.Animation.RootFrame, null!, 0, 0
			)
		);
		Assert.Throws<NotSupportedException>(
			() => first.Animation.UpdateFrameRegionAsync(
				first.Animation.RootFrame, indexed, 0, 0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => first.Animation.UpdateFrameRegionAsync(
				first.Animation.RootFrame, pixel, 2, 0
			)
		);
		Assert.Throws<ArgumentException>(
			() => first.Animation.UpdateFrameRegionAsync(
				second.Animation.RootFrame, pixel, 0, 0
			)
		);
		Assert.Throws<OperationCanceledException>(
			() => first.Animation.UpdateFrameRegionAsync(
				first.Animation.RootFrame, pixel, 0, 0, cancelled.Token
			)
		);
		Assert.Equal( writesBefore, transport.Writes.Count );
	}

	[Fact]
	public async Task PartialFrameRejectionPreservesOwnershipButMissingIdentityInvalidatesResource() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterImage pixel = TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] );

		Task<TerminalControlMutationResult> invalid = resource.Animation
			.UpdateFrameRegionAsync( resource.Animation.RootFrame, pixel, 0, 0 ).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;EINVAL:invalid region\u001b\\" ) );
		TerminalControlMutationResult invalidResult = await invalid;
		Assert.Equal( TerminalControlStatus.Failed, invalidResult.Status );
		Assert.Equal(
			TerminalControlMutationConfirmation.Unspecified,
			invalidResult.Confirmation
		);
		Assert.Equal( TerminalRasterOwnershipStatus.Current, resource.OwnershipState.Status );
		AssertOperationUnknown(
			session,
			TerminalRasterOperation.FrameRegionUpdateRgb24
		);

		Task<TerminalControlMutationResult> missing = resource.Animation
			.UpdateFrameRegionAsync( resource.Animation.RootFrame, pixel, 0, 0 ).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;ENOENT:missing frame\u001b\\" ) );
		TerminalControlMutationResult missingResult = await missing;
		Assert.Equal( TerminalControlStatus.Unavailable, missingResult.Status );
		Assert.Equal(
			TerminalControlMutationConfirmation.Unspecified,
			missingResult.Confirmation
		);
		Assert.Equal( TerminalRasterOwnershipStatus.Stale, resource.OwnershipState.Status );
		AssertOperationUnknown(
			session,
			TerminalRasterOperation.FrameRegionUpdateRgb24
		);
	}

	[Fact]
	public async Task ConcurrentPartialUpdatesSerializeAndCommittedFailurePreservesFrameIdentity() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterImage pixel = TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] );
		Task<TerminalControlMutationResult> first = resource.Animation
			.UpdateFrameRegionAsync( resource.Animation.RootFrame, pixel, 0, 0 ).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Task<TerminalControlMutationResult> second = resource.Animation
			.UpdateFrameRegionAsync( resource.Animation.RootFrame, pixel, 0, 0 ).AsTask();
		await YieldSeveralTimesAsync();
		Assert.Equal( 2, transport.Writes.Count );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await first ).Succeeded );
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await second ).Succeeded );

		transport.FailNextFlush = true;
		await Assert.ThrowsAsync<IOException>(
			() => resource.Animation.UpdateFrameRegionAsync(
				resource.Animation.RootFrame, pixel, 0, 0
			).AsTask()
		);
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
		Assert.Equal( TerminalRasterOwnershipStatus.Current, resource.OwnershipState.Status );
	}

	[Fact]
	public async Task SessionGenerationLossRejectsPartialFrameUpdateWithoutOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		session.InvalidateState();
		int writesBefore = transport.Writes.Count;

		TerminalControlMutationResult result = await resource.Animation.UpdateFrameRegionAsync(
			resource.Animation.RootFrame,
			TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ),
			0,
			0
		);

		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal( writesBefore, transport.Writes.Count );
		Assert.Equal( TerminalRasterAnimationStatus.Stale, resource.Animation.State.Status );
	}

	[Fact]
	public async Task ResourceDisposalWhilePartialUpdateWaitsForOutputGateEmitsNoEdit() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		IDisposable outputLease = await session.AcquireControlOutputAsync(
			CancellationToken.None
		);

		Task<TerminalControlMutationResult> update = resource.Animation
			.UpdateFrameRegionAsync(
				resource.Animation.RootFrame,
				TerminalRasterImage.CreateRgb24( 1, 1, [ 1, 2, 3 ] ),
				0,
				0
			).AsTask();
		await YieldSeveralTimesAsync();
		Assert.False( update.IsCompleted );
		Assert.Single( transport.Writes );

		Task disposal = resource.DisposeAsync().AsTask();
		await YieldSeveralTimesAsync();
		Assert.False( disposal.IsCompleted );
		outputLease.Dispose();

		TerminalControlMutationResult result = await update;
		await disposal;
		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal(
			TerminalControlMutationConfirmation.Unspecified,
			result.Confirmation
		);
		Assert.DoesNotContain(
			transport.Writes,
			static value => Encoding.ASCII.GetString( value ).StartsWith(
				"\u001b_Ga=f,",
				StringComparison.Ordinal
			)
		);
	}

	[Fact]
	public async Task RootFrameDurationCompletesAfterOutputWithoutTerminalResponse() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);

		Task<TerminalControlMutationResult> control = resource.Animation
			.SetFrameDurationAsync(
				resource.Animation.RootFrame,
				TimeSpan.FromMilliseconds( 55 )
			)
			.AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,r=1,z=55,q=2\u001b\\" ),
			transport.Writes[ 1 ]
		);

		TerminalControlMutationResult result = await AwaitWithoutResponseAsync( control );
		Assert.True( result.Succeeded );
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			result.Confirmation
		);
	}

	[Fact]
	public async Task AppendedFrameSupportsDurationAndCurrentSelectionByOpaqueToken() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport,
			expectedWriteCount: 2
		);

		Task<TerminalControlMutationResult> duration = resource.Animation
			.SetFrameDurationAsync(
				frame,
				TimeSpan.FromMilliseconds( 75 )
			)
			.AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,r=2,z=75,q=2\u001b\\" ),
			transport.Writes[ 2 ]
		);
		TerminalControlMutationResult durationResult = await AwaitWithoutResponseAsync(
			duration
		);
		Assert.True( durationResult.Succeeded );
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			durationResult.Confirmation
		);

		Task<TerminalControlMutationResult> selection = resource.Animation
			.SelectFrameAsync( frame )
			.AsTask();
		await transport.WaitForWriteCountAsync( 4 );
		Assert.Equal(
			Encoding.ASCII.GetBytes( "\u001b_Ga=a,i=77,c=2,q=2\u001b\\" ),
			transport.Writes[ 3 ]
		);
		TerminalControlMutationResult selectionResult = await AwaitWithoutResponseAsync(
			selection
		);
		Assert.True( selectionResult.Succeeded );
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			selectionResult.Confirmation
		);
	}

	[Fact]
	public async Task CompositionOfKnownFramesWaitsForAcknowledgementAndUsesOpaqueTokens() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);

		Task<TerminalControlMutationResult> composition = resource.Animation
			.ComposeFrameAsync(
				resource.Animation.RootFrame,
				destination,
				new TerminalRasterSourceRectangle( 0, 0, 1, 1 ),
				destinationX: 0,
				destinationY: 0,
				TerminalRasterFrameCompositionMode.Replace
			).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Assert.False( composition.IsCompleted );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=c,i=77,r=1,c=2,w=1,h=1,X=0,Y=0,x=0,y=0,C=1\u001b\\"
			),
			transport.Writes[ 2 ]
		);
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await composition ).Succeeded );
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
		Assert.Equal( 2, destination.SequenceNumber );
		Assert.Equal(
			TerminalCapabilitySupport.Verified,
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameComposition
			).Support
		);
		Assert.Equal(
			TerminalCapabilitySupport.Unknown,
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameRegionUpdateRgb24
			).Support
		);
	}

	[Fact]
	public async Task AppendedSourceComposesIntoRootAtResourceEdge() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u, width: 2, height: 2
		);
		Task<TerminalControlResult<TerminalRasterAnimationFrame>> append = resource.Animation
			.AddFrameAsync(
				TerminalRasterImage.CreateRgb24( 2, 2, new byte[12] ),
				TimeSpan.FromMilliseconds( 40 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		TerminalRasterAnimationFrame source = Assert.IsType<TerminalRasterAnimationFrame>(
			( await append ).Value
		);

		Task<TerminalControlMutationResult> composition = resource.Animation
			.ComposeFrameAsync(
				source, resource.Animation.RootFrame,
				new TerminalRasterSourceRectangle( 1, 1, 1, 1 ),
				1, 1
			).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=c,i=77,r=2,c=1,w=1,h=1,X=1,Y=1,x=1,y=1\u001b\\"
			),
			transport.Writes[ 2 ]
		);
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await composition ).Succeeded );
	}

	[Fact]
	public async Task SampleCompositionPathExecutesAgainstScriptedTerminal() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u, width: 2, height: 2
		);
		Task<TerminalControlResult<TerminalRasterAnimationFrame>> append =
			resource.Animation.AddFrameAsync(
				TerminalRasterImage.CreateRgb24( 2, 2, new byte[12] ),
				TimeSpan.FromMilliseconds( 180 )
			).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		TerminalRasterAnimationFrame destination = Assert.IsType<TerminalRasterAnimationFrame>(
			( await append ).Value
		);

		Task<TerminalControlMutationResult> composition =
			Icod.Terminal.RasterAnimation.Sample.RasterAnimationCompositionExample
				.ComposeAsync( resource.Animation, destination ).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=c,i=77,r=1,c=2,w=1,h=1,X=0,Y=0,x=1,y=1,C=1\u001b\\"
			),
			transport.Writes[ 2 ]
		);
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await composition ).Succeeded );
	}

	[Fact]
	public async Task SamplePartialUpdatePathExecutesAgainstScriptedTerminal() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u, width: 2, height: 2
		);

		Task<TerminalControlMutationResult> update =
			Icod.Terminal.RasterAnimation.Sample.RasterAnimationCompositionExample
				.UpdateRegionAsync(
					resource.Animation,
					resource.Animation.RootFrame
				).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=f,f=32,s=1,v=1,t=d,i=77,r=1,x=0,y=1,X=1,m=0;IOCg/w==\u001b\\"
			),
			transport.Writes[ 1 ]
		);
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await update ).Succeeded );
		Assert.Equal(
			TerminalCapabilitySupport.Verified,
			session.InspectRasterOperation(
				TerminalRasterOperation.FrameRegionUpdateRgba32
			).Support
		);
	}

	[Fact]
	public async Task SamplePreflightAllowsFirstAnimationCommandWithUnknownAnimationEvidence() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		Assert.False( session.InspectCapability(
			TerminalCapability.PersistentRasterAnimation
		).IsUsable );
		Assert.True( await Icod.Terminal.RasterAnimation.Sample
			.RasterAnimationCompositionExample.VerifyPrerequisiteAsync( session ) );
		Assert.Empty( transport.Writes );

		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		Task<TerminalControlMutationResult> firstControl = resource.Animation
			.SetFrameDurationAsync(
				resource.Animation.RootFrame,
				TimeSpan.FromMilliseconds( 180 )
				).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		TerminalControlMutationResult firstResult = await AwaitWithoutResponseAsync(
			firstControl
		);
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			firstResult.Confirmation
		);
		Assert.False( session.InspectCapability(
			TerminalCapability.PersistentRasterAnimation
		).IsUsable );
	}

	[Fact]
	public async Task CompositionRejectsForeignAndOutOfBoundsFramesBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource first = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		await using TerminalRasterResource second = await CreateResourceAsync(
			session, transport, imageId: 88u, expectedWriteCount: 2
		);
		int writesBefore = transport.Writes.Count;
		TerminalRasterSourceRectangle pixel = new( 0, 0, 1, 1 );

		Assert.Throws<ArgumentException>(
			() => first.Animation.ComposeFrameAsync(
				first.Animation.RootFrame,
				second.Animation.RootFrame,
				pixel, 0, 0
			)
		);
		Assert.Throws<ArgumentNullException>(
			() => first.Animation.ComposeFrameAsync(
				null!, first.Animation.RootFrame, pixel, 0, 0
			)
		);
		Assert.Throws<ArgumentOutOfRangeException>(
			() => first.Animation.ComposeFrameAsync(
				first.Animation.RootFrame,
				first.Animation.RootFrame,
				pixel, 1, 0
			)
		);
		Assert.Throws<ArgumentException>(
			() => first.Animation.ComposeFrameAsync(
				first.Animation.RootFrame,
				first.Animation.RootFrame,
				pixel, 0, 0
			)
		);
		Assert.Equal( writesBefore, transport.Writes.Count );
	}

	[Fact]
	public async Task UnsupportedAnimationBackendRejectsCompositionBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u, width: 2, height: 2
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyPersistentRasterAnimation,
			TerminalCapabilitySupportState.Unsupported,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		int writesBefore = transport.Writes.Count;
		TerminalControlMutationResult result = await resource.Animation.ComposeFrameAsync(
			resource.Animation.RootFrame, resource.Animation.RootFrame,
			new TerminalRasterSourceRectangle( 0, 0, 1, 1 ), 1, 1
		);
		Assert.Equal( TerminalControlStatus.Unsupported, result.Status );
		Assert.Equal( writesBefore, transport.Writes.Count );
	}

	[Fact]
	public async Task CompositionRejectionPreservesFramesButMissingIdentityInvalidatesResource() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		TerminalRasterSourceRectangle pixel = new( 0, 0, 1, 1 );

		Task<TerminalControlMutationResult> invalid = resource.Animation
			.ComposeFrameAsync( resource.Animation.RootFrame, destination, pixel, 0, 0 )
			.AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;EINVAL:invalid frame\u001b\\" )
		);
		Assert.Equal( TerminalControlStatus.Failed, ( await invalid ).Status );
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
		AssertOperationUnknown( session, TerminalRasterOperation.FrameComposition );

		Task<TerminalControlMutationResult> storage = resource.Animation
			.ComposeFrameAsync( resource.Animation.RootFrame, destination, pixel, 0, 0 )
			.AsTask();
		await transport.WaitForWriteCountAsync( 4 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;ENOSPC:frame storage\u001b\\" )
		);
		Assert.Equal( TerminalControlStatus.Failed, ( await storage ).Status );
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
		AssertOperationUnknown( session, TerminalRasterOperation.FrameComposition );

		Task<TerminalControlMutationResult> missing = resource.Animation
			.ComposeFrameAsync( resource.Animation.RootFrame, destination, pixel, 0, 0 )
			.AsTask();
		await transport.WaitForWriteCountAsync( 5 );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;ENOENT:missing frame\u001b\\" )
		);
		Assert.Equal( TerminalControlStatus.Unavailable, ( await missing ).Status );
		Assert.Equal( TerminalRasterOwnershipStatus.Stale, resource.OwnershipState.Status );
		AssertOperationUnknown( session, TerminalRasterOperation.FrameComposition );
	}

	[Fact]
	public async Task MalformedCorrelatedCompositionReplyCannotReportSuccess() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		Task<TerminalControlMutationResult> composition = resource.Animation
			.ComposeFrameAsync(
				resource.Animation.RootFrame, destination,
				new TerminalRasterSourceRectangle( 0, 0, 1, 1 ), 0, 0
			).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;\u001b\\" ) );
		await Assert.ThrowsAsync<FormatException>( () => composition );
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
		AssertOperationUnknown( session, TerminalRasterOperation.FrameComposition );
	}

	[Fact]
	public async Task SameFrameNonoverlapComposesButOverlapAndSourceOverflowReject() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u, width: 2, height: 2
		);
		TerminalRasterAnimationFrame root = resource.Animation.RootFrame;

		Assert.Throws<ArgumentOutOfRangeException>(
			() => resource.Animation.ComposeFrameAsync(
				root, root,
				new TerminalRasterSourceRectangle( 1, 1, 2, 1 ),
				0, 0
			)
		);
		Assert.Throws<ArgumentException>(
			() => resource.Animation.ComposeFrameAsync(
				root, root,
				new TerminalRasterSourceRectangle( 0, 0, 2, 1 ),
				0, 0
			)
		);

		Task<TerminalControlMutationResult> composition = resource.Animation
			.ComposeFrameAsync(
				root, root,
				new TerminalRasterSourceRectangle( 0, 0, 1, 1 ),
				1, 1
			).AsTask();
		await transport.WaitForWriteCountAsync( 2 );
		Assert.Equal(
			Encoding.ASCII.GetBytes(
				"\u001b_Ga=c,i=77,r=1,c=1,w=1,h=1,X=0,Y=0,x=1,y=1\u001b\\"
			),
			transport.Writes[ 1 ]
		);
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await composition ).Succeeded );
	}

	[Fact]
	public async Task CompositionCancellationBeforeOutputAndDisposedResourceDoNotWrite() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimation animation = resource.Animation;
		TerminalRasterAnimationFrame root = animation.RootFrame;
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		TerminalRasterSourceRectangle pixel = new( 0, 0, 1, 1 );
		using CancellationTokenSource cancelled = new();
		cancelled.Cancel();
		Assert.Throws<OperationCanceledException>(
			() => animation.ComposeFrameAsync(
				root, destination, pixel, 0, 0,
				cancellationToken: cancelled.Token
			)
		);
		Assert.Equal( 2, transport.Writes.Count );

		await resource.DisposeAsync();
		int writesAfterDisposal = transport.Writes.Count;
		Assert.Throws<ObjectDisposedException>(
			() => animation.ComposeFrameAsync(
				root, destination, pixel, 0, 0
			)
		);
		Assert.Equal( writesAfterDisposal, transport.Writes.Count );
		AssertOperationUnknown( session, TerminalRasterOperation.FrameComposition );
	}

	[Fact]
	public async Task ConcurrentCompositionSerializesAcknowledgements() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		TerminalRasterSourceRectangle pixel = new( 0, 0, 1, 1 );
		Task<TerminalControlMutationResult> first = resource.Animation.ComposeFrameAsync(
			resource.Animation.RootFrame, destination, pixel, 0, 0
		).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Task<TerminalControlMutationResult> second = resource.Animation.ComposeFrameAsync(
			resource.Animation.RootFrame, destination, pixel, 0, 0
		).AsTask();
		await YieldSeveralTimesAsync();
		Assert.Equal( 3, transport.Writes.Count );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await first ).Succeeded );
		await transport.WaitForWriteCountAsync( 4 );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await second ).Succeeded );
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
	}

	[Fact]
	public async Task CompositionSerializesQueuedAppendAndPlayback() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		Task<TerminalControlMutationResult> composition = resource.Animation
			.ComposeFrameAsync(
				resource.Animation.RootFrame, destination,
				new TerminalRasterSourceRectangle( 0, 0, 1, 1 ), 0, 0
			).AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		Task<TerminalControlResult<TerminalRasterAnimationFrame>> append =
			resource.Animation.AddFrameAsync(
				TerminalRasterImage.CreateRgb24( 1, 1, [ 7, 8, 9 ] ),
				TimeSpan.FromMilliseconds( 40 )
			).AsTask();
		Task<TerminalControlMutationResult> playback = resource.Animation.RunAsync()
			.AsTask();
		TerminalControlMutationResult playbackResult = await AwaitWithoutResponseAsync(
			playback
		);
		await transport.WaitForWriteCountAsync( 4 );
		Assert.StartsWith( "\u001b_Ga=a,", Encoding.ASCII.GetString( transport.Writes[ 3 ] ) );
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			playbackResult.Confirmation
		);

		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.True( ( await composition ).Succeeded );
		await transport.WaitForWriteCountAsync( 5 );
		Assert.StartsWith( "\u001b_Ga=f,", Encoding.ASCII.GetString( transport.Writes[ 4 ] ) );
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );
		Assert.Equal( 3, Assert.IsType<TerminalRasterAnimationFrame>(
			( await append ).Value
		).SequenceNumber );
	}

	[Fact]
	public async Task SessionGenerationLossRejectsCompositionWithoutOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		session.InvalidateState();
		int writesBefore = transport.Writes.Count;
		TerminalControlMutationResult result = await resource.Animation.ComposeFrameAsync(
			resource.Animation.RootFrame, destination,
			new TerminalRasterSourceRectangle( 0, 0, 1, 1 ), 0, 0
		);
		Assert.Equal( TerminalControlStatus.Unavailable, result.Status );
		Assert.Equal( writesBefore, transport.Writes.Count );
		Assert.Equal( TerminalRasterAnimationStatus.Stale, resource.Animation.State.Status );
	}

	[Fact]
	public async Task LateCompositionAcknowledgementCannotSeedNextEvidenceGeneration() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		Task<TerminalControlMutationResult> composition = resource.Animation.ComposeFrameAsync(
			resource.Animation.RootFrame,
			destination,
			new TerminalRasterSourceRectangle( 0, 0, 1, 1 ),
			0,
			0
		).AsTask();
		await transport.WaitForWriteCountAsync( 3 );

		session.InvalidateState();
		transport.Publish( Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" ) );

		Assert.Equal( TerminalControlStatus.Unavailable, ( await composition ).Status );
		AssertOperationUnknown( session, TerminalRasterOperation.FrameComposition );
	}

	[Fact]
	public async Task CommittedCompositionFlushFailureLeavesPixelsUncertainAndFramesKnown() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session, transport, imageId: 77u
		);
		TerminalRasterAnimationFrame destination = await AppendFrameAsync(
			resource, transport, expectedWriteCount: 2
		);
		transport.FailNextFlush = true;
		await Assert.ThrowsAsync<IOException>(
			() => resource.Animation.ComposeFrameAsync(
				resource.Animation.RootFrame, destination,
				new TerminalRasterSourceRectangle( 0, 0, 1, 1 ), 0, 0
			).AsTask()
		);
		Assert.Equal( 3, transport.Writes.Count );
		Assert.Equal( TerminalRasterAnimationStatus.Current, resource.Animation.State.Status );
		Assert.Equal( TerminalRasterOwnershipStatus.Current, resource.OwnershipState.Status );
		AssertOperationUnknown( session, TerminalRasterOperation.FrameComposition );
	}

	[Fact]
	public async Task ForeignAnimationFrameRejectsBeforeOutput() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource first = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		await using TerminalRasterResource second = await CreateResourceAsync(
			session,
			transport,
			imageId: 88u,
			expectedWriteCount: 2
		);
		int writesBefore = transport.Writes.Count;

		Assert.Throws<ArgumentException>(
			() => first.Animation.SelectFrameAsync(
				second.Animation.RootFrame
			)
		);
		Assert.Throws<ArgumentException>(
			() => first.Animation.SetFrameDurationAsync(
				second.Animation.RootFrame,
				TimeSpan.FromMilliseconds( 40 )
			)
		);
		Assert.Equal( writesBefore, transport.Writes.Count );
	}

	[Fact]
	public async Task UnexpectedFrameControlReplyDoesNotPoisonKnownFrameSequence() {
		ScriptedTransport transport = new();
		await using TerminalSession session = await OpenSessionAsync( transport );
		await using TerminalRasterResource resource = await CreateResourceAsync(
			session,
			transport,
			imageId: 77u
		);
		TerminalRasterAnimationFrame frame = await AppendFrameAsync(
			resource,
			transport,
			expectedWriteCount: 2
		);

		Task<TerminalControlMutationResult> selection = resource.Animation
			.SelectFrameAsync( frame )
			.AsTask();
		await transport.WaitForWriteCountAsync( 3 );
		TerminalControlMutationResult selectionResult = await AwaitWithoutResponseAsync(
			selection
		);
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			selectionResult.Confirmation
		);
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;EINVAL:unexpected reply\u001b\\" )
		);
		await YieldSeveralTimesAsync();
		Assert.Equal(
			new TerminalRasterAnimationState(
				TerminalRasterAnimationStatus.Current,
				TerminalRasterAnimationLossReason.None
			),
			resource.Animation.State
		);

		Task<TerminalControlMutationResult> retry = resource.Animation
			.SetFrameDurationAsync(
				frame,
				TimeSpan.FromMilliseconds( 90 )
			)
			.AsTask();
		await transport.WaitForWriteCountAsync( 4 );
		Assert.Equal(
			TerminalControlMutationConfirmation.OutputCommitted,
			( await AwaitWithoutResponseAsync( retry ) ).Confirmation
		);
	}

	private static async Task<TerminalRasterAnimationFrame> AppendFrameAsync(
		TerminalRasterResource resource,
		ScriptedTransport transport,
		int expectedWriteCount
	) {
		Task<TerminalControlResult<TerminalRasterAnimationFrame>> append =
			resource.Animation.AddFrameAsync(
				TerminalRasterImage.CreateRgb24(
					1,
					1,
					[ 4, 5, 6 ]
				),
				TimeSpan.FromMilliseconds( 40 )
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes( "\u001b_Gi=77;OK\u001b\\" )
		);
		TerminalControlResult<TerminalRasterAnimationFrame> result = await append;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterAnimationFrame>( result.Value );
	}

	private static void AssertOperationUnknown(
		TerminalSession session,
		TerminalRasterOperation operation
	) {
		TerminalRasterOperationStatus status = session.InspectRasterOperation( operation );
		Assert.Equal( TerminalCapabilitySupport.Unknown, status.Support );
		Assert.Equal( TerminalCapabilityEvidenceKind.None, status.EvidenceKind );
	}

	private static async Task<TerminalRasterResource> CreateResourceAsync(
		TerminalSession session,
		ScriptedTransport transport,
		uint imageId,
		int expectedWriteCount = 1,
		int width = 1,
		int height = 1
	) {
		Task<TerminalControlResult<TerminalRasterResource>> creation =
			session.CreateRasterResourceAsync(
				TerminalRasterImage.CreateRgb24(
					width,
					height,
					new byte[checked( width * height * 3 )]
				)
			).AsTask();
		await transport.WaitForWriteCountAsync( expectedWriteCount );
		transport.Publish(
			Encoding.ASCII.GetBytes(
				$"\u001b_Gi={imageId},I={expectedWriteCount};OK\u001b\\"
			)
		);
		TerminalControlResult<TerminalRasterResource> result = await creation;
		Assert.Equal( TerminalControlStatus.Available, result.Status );
		return Assert.IsType<TerminalRasterResource>( result.Value );
	}

	private static async ValueTask<TerminalSession> OpenSessionAsync(
		ScriptedTransport transport
	) {
		TerminalSession session = await TerminalSession.OpenAsync(
			new RecordingTerminalControlProvider(),
			TerminalEndpoint.StandardInput,
			TerminalEndpoint.StandardOutput,
			transport,
			transport,
			new TerminalSessionOptions {
				TerminalOverride = TerminalProfiles.Dumb,
				ConfigureOutput = false,
				MonotonicClock = new FrozenMonotonicClock(),
				ObserveLifecycleEvents = false,
				RequireInteractiveOutput = false
			}
		);
		session.RecordSemanticBackendEvidence(
			TerminalProtocolBackend.ApcKittyGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		session.RecordSemanticOperationEvidence(
			TerminalSemanticOperation.PersistentRasterGraphics,
			TerminalCapabilitySupportState.Verified,
			TerminalCapabilityEvidenceSource.ProtocolResponse
		);
		return session;
	}

	private static async Task YieldSeveralTimesAsync() {
		for ( int iteration = 0; iteration < 8; ++iteration ) {
			await Task.Yield();
		}
	}

	private static async Task<T> AwaitWithoutResponseAsync<T>(
		Task<T> task
	) {
		Task completed = await Task.WhenAny(
			task,
			Task.Delay( TimeSpan.FromMilliseconds( 250 ) )
		);
		Assert.Same( task, completed );
		return await task;
	}

	private sealed class ScriptedTransport : ITerminalInput, ITerminalOutput {
		private readonly Channel<byte[]> input = Channel.CreateUnbounded<byte[]>(
			new UnboundedChannelOptions {
				SingleReader = true,
				SingleWriter = false,
				AllowSynchronousContinuations = false
			}
		);
		private readonly object synchronization = new();
		private readonly SemaphoreSlim writeSignal = new( 0 );
		private readonly List<byte[]> writes = [];

		internal bool FailNextFlush {
			get;
			set;
		}

		internal IReadOnlyList<byte[]> Writes {
			get {
				lock ( this.synchronization ) {
					return this.writes.Select(
						static value => value.ToArray()
					).ToArray();
				}
			}
		}

		public async ValueTask<int> ReadAsync(
			Memory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			byte[] value = await this.input.Reader.ReadAsync(
				cancellationToken
			).ConfigureAwait( false );
			if ( value.Length > buffer.Length ) {
				throw new InvalidOperationException(
					"The scripted response exceeds the terminal input buffer."
				);
			}
			value.AsSpan().CopyTo( buffer.Span );
			return value.Length;
		}

		public ValueTask WriteAsync(
			ReadOnlyMemory<byte> buffer,
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			lock ( this.synchronization ) {
				this.writes.Add( buffer.ToArray() );
			}
			this.writeSignal.Release();
			return ValueTask.CompletedTask;
		}

		public ValueTask FlushAsync(
			CancellationToken cancellationToken = default
		) {
			cancellationToken.ThrowIfCancellationRequested();
			if ( this.FailNextFlush ) {
				this.FailNextFlush = false;
				throw new IOException( "Synthetic composition flush failure." );
			}
			return ValueTask.CompletedTask;
		}

		internal void Publish(
			byte[] bytes
		) {
			ArgumentNullException.ThrowIfNull( bytes );
			if ( !this.input.Writer.TryWrite( bytes.ToArray() ) ) {
				throw new InvalidOperationException(
					"The scripted terminal input channel is closed."
				);
			}
		}

		internal async Task WaitForWriteCountAsync(
			int expected
		) {
			if ( 0 > expected ) {
				throw new ArgumentOutOfRangeException( nameof( expected ) );
			}

			using CancellationTokenSource timeout = new(
				TimeSpan.FromSeconds( 5 )
			);
			while ( this.Writes.Count < expected ) {
				await this.writeSignal.WaitAsync(
					timeout.Token
				).ConfigureAwait( false );
			}
		}
	}
}
