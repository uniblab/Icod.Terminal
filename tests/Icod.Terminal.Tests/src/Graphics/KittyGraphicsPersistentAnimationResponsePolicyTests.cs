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

using Icod.Terminal;
using Xunit;

public sealed class KittyGraphicsPersistentAnimationResponsePolicyTests {
	[Theory]
	[InlineData(
		(int)KittyGraphicsPersistentAnimationOperation.FullFrameTransfer,
		(int)KittyGraphicsPersistentAnimationResponsePolicy.Required
	)]
	[InlineData(
		(int)KittyGraphicsPersistentAnimationOperation.RegionalFrameTransfer,
		(int)KittyGraphicsPersistentAnimationResponsePolicy.Required
	)]
	[InlineData(
		(int)KittyGraphicsPersistentAnimationOperation.AnimationControl,
		(int)KittyGraphicsPersistentAnimationResponsePolicy.None
	)]
	[InlineData(
		(int)KittyGraphicsPersistentAnimationOperation.FrameComposition,
		(int)KittyGraphicsPersistentAnimationResponsePolicy.Optional
	)]
	public void SemanticOperationMapsToPublishedResponseClass(
		int operation,
		int expected
	) {
		Assert.Equal(
			(KittyGraphicsPersistentAnimationResponsePolicy)expected,
			KittyGraphicsPersistentAnimationResponsePolicies.For(
				(KittyGraphicsPersistentAnimationOperation)operation
			)
		);
	}

	[Fact]
	public void UnknownSemanticOperationFailsClosed() {
		Assert.Throws<ArgumentOutOfRangeException>(
			() => KittyGraphicsPersistentAnimationResponsePolicies.For(
				(KittyGraphicsPersistentAnimationOperation)int.MaxValue
			)
		);
	}
}
