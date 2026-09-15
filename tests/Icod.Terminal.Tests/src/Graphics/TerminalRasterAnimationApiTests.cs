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

using System.Reflection;
using Icod.Terminal;
using Xunit;

/// <summary>
/// Freezes the additive 1.16 persistent-raster animation public surface.
/// </summary>
public sealed class TerminalRasterAnimationApiTests {
	[Fact]
	public void AnimationPublicSurfaceHasFrozenSemanticShape() {
		Assert.Equal( 11, (int)TerminalCapability.PersistentRasterAnimation );

		Assert.Equal(
			[
				nameof( TerminalRasterAnimationStatus.Current ),
				nameof( TerminalRasterAnimationStatus.SequenceUncertain ),
				nameof( TerminalRasterAnimationStatus.Stale ),
				nameof( TerminalRasterAnimationStatus.Released ),
				nameof( TerminalRasterAnimationStatus.OwnerDisposed )
			],
			Enum.GetNames<TerminalRasterAnimationStatus>()
		);
		Assert.Equal(
			[
				nameof( TerminalRasterAnimationLossReason.None ),
				nameof( TerminalRasterAnimationLossReason.FrameSequenceAmbiguous ),
				nameof( TerminalRasterAnimationLossReason.SessionStateLost ),
				nameof( TerminalRasterAnimationLossReason.ResourceMissing ),
				nameof( TerminalRasterAnimationLossReason.ResourceReleased ),
				nameof( TerminalRasterAnimationLossReason.ExplicitResourceDisposal )
			],
			Enum.GetNames<TerminalRasterAnimationLossReason>()
		);

		TerminalRasterAnimationState state = new(
			TerminalRasterAnimationStatus.Current,
			TerminalRasterAnimationLossReason.None
		);
		Assert.Equal( TerminalRasterAnimationStatus.Current, state.Status );
		Assert.Equal( TerminalRasterAnimationLossReason.None, state.LossReason );

		PropertyInfo repeatCount = Assert.IsAssignableFrom<PropertyInfo>(
			typeof( TerminalRasterAnimationPlaybackOptions ).GetProperty(
				nameof( TerminalRasterAnimationPlaybackOptions.RepeatCount )
			)
		);
		Assert.Equal( typeof( int? ), repeatCount.PropertyType );
		Assert.True( repeatCount.CanRead );
		Assert.True( repeatCount.CanWrite );

		Type animationType = typeof( TerminalRasterAnimation );
		Type frameType = typeof( TerminalRasterAnimationFrame );
		Assert.Empty(
			animationType.GetConstructors(
				BindingFlags.Instance | BindingFlags.Public
			)
		);
		Assert.Empty(
			frameType.GetConstructors(
				BindingFlags.Instance | BindingFlags.Public
			)
		);
		Assert.False( typeof( IDisposable ).IsAssignableFrom( animationType ) );
		Assert.False( typeof( IAsyncDisposable ).IsAssignableFrom( animationType ) );
		Assert.False( typeof( IDisposable ).IsAssignableFrom( frameType ) );
		Assert.False( typeof( IAsyncDisposable ).IsAssignableFrom( frameType ) );

		PropertyInfo resourceAnimation = Assert.IsAssignableFrom<PropertyInfo>(
			typeof( TerminalRasterResource ).GetProperty(
				nameof( TerminalRasterResource.Animation )
			)
		);
		Assert.Equal( animationType, resourceAnimation.PropertyType );
		Assert.True( resourceAnimation.CanRead );
		Assert.False( resourceAnimation.CanWrite );

		PropertyInfo rootFrame = Assert.IsAssignableFrom<PropertyInfo>(
			animationType.GetProperty( nameof( TerminalRasterAnimation.RootFrame ) )
		);
		Assert.Equal( frameType, rootFrame.PropertyType );
		Assert.False( rootFrame.CanWrite );

		PropertyInfo animationState = Assert.IsAssignableFrom<PropertyInfo>(
			animationType.GetProperty( nameof( TerminalRasterAnimation.State ) )
		);
		Assert.Equal( typeof( TerminalRasterAnimationState ), animationState.PropertyType );
		Assert.False( animationState.CanWrite );
	}

	[Fact]
	public void AnimationMethodsHaveFrozenShape() {
		Type animationType = typeof( TerminalRasterAnimation );
		Type frameType = typeof( TerminalRasterAnimationFrame );

		MethodInfo addFrame = Assert.IsAssignableFrom<MethodInfo>(
			animationType.GetMethod(
				nameof( TerminalRasterAnimation.AddFrameAsync ),
				[
					typeof( TerminalRasterImage ),
					typeof( TimeSpan ),
					typeof( CancellationToken )
				]
			)
		);
		Assert.Equal(
			typeof( ValueTask<TerminalControlResult<TerminalRasterAnimationFrame>> ),
			addFrame.ReturnType
		);

		MethodInfo setDuration = Assert.IsAssignableFrom<MethodInfo>(
			animationType.GetMethod(
				nameof( TerminalRasterAnimation.SetFrameDurationAsync ),
				[
					frameType,
					typeof( TimeSpan ),
					typeof( CancellationToken )
				]
			)
		);
		Assert.Equal(
			typeof( ValueTask<TerminalControlMutationResult> ),
			setDuration.ReturnType
		);

		MethodInfo selectFrame = Assert.IsAssignableFrom<MethodInfo>(
			animationType.GetMethod(
				nameof( TerminalRasterAnimation.SelectFrameAsync ),
				[
					frameType,
					typeof( CancellationToken )
				]
			)
		);
		Assert.Equal(
			typeof( ValueTask<TerminalControlMutationResult> ),
			selectFrame.ReturnType
		);

		MethodInfo stop = Assert.IsAssignableFrom<MethodInfo>(
			animationType.GetMethod(
				nameof( TerminalRasterAnimation.StopAsync ),
				[ typeof( CancellationToken ) ]
			)
		);
		Assert.Equal( typeof( ValueTask<TerminalControlMutationResult> ), stop.ReturnType );

		MethodInfo runLoading = Assert.IsAssignableFrom<MethodInfo>(
			animationType.GetMethod(
				nameof( TerminalRasterAnimation.RunLoadingAsync ),
				[ typeof( CancellationToken ) ]
			)
		);
		Assert.Equal(
			typeof( ValueTask<TerminalControlMutationResult> ),
			runLoading.ReturnType
		);

		MethodInfo run = Assert.IsAssignableFrom<MethodInfo>(
			animationType.GetMethod(
				nameof( TerminalRasterAnimation.RunAsync ),
				[
					typeof( TerminalRasterAnimationPlaybackOptions ),
					typeof( CancellationToken )
				]
			)
		);
		Assert.Equal( typeof( ValueTask<TerminalControlMutationResult> ), run.ReturnType );
	}

	[Fact]
	public void AnimationSurfaceDoesNotExposeProtocolIdentityVocabulary() {
		string[] forbiddenFragments = [
			"ImageId",
			"ImageNumber",
			"FrameNumber",
			"Generation",
			"Kitty",
			"Apc",
			"Action"
		];

		Type[] publicTypes = [
			typeof( TerminalRasterAnimationState ),
			typeof( TerminalRasterAnimationPlaybackOptions ),
			typeof( TerminalRasterAnimation ),
			typeof( TerminalRasterAnimationFrame )
		];
		foreach ( Type type in publicTypes ) {
			foreach ( MemberInfo member in type.GetMembers(
				BindingFlags.Instance | BindingFlags.Public
				| BindingFlags.Static
			) ) {
				Assert.DoesNotContain(
					forbiddenFragments,
					fragment => member.Name.Contains(
						fragment,
						StringComparison.OrdinalIgnoreCase
					)
				);
			}
		}
	}
}
