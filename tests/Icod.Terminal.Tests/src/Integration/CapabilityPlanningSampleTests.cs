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
namespace Icod.Terminal.Tests.Integration;

using System.Reflection;
using Xunit;

public sealed class CapabilityPlanningSampleTests {
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task ActualSampleReportsAndVerifiesWithoutMutatingPresentation(bool outputAvailable) =>
        Icod.Terminal.CapabilityPlanning.Smoke.CapabilityPlanningScenario.RunAsync(outputAvailable);

    [Fact]
    public async Task PreCancelledCliReturns130WithoutOpeningTerminal() {
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using StringWriter output = new();
        using StringWriter error = new();
        Assert.Equal(130, await Icod.Terminal.CapabilityPlanning.Sample.CapabilityPlanningExample.RunAsync([], output, error, cancellation.Token));
        Assert.Contains("Cancelled", error.ToString());
    }
    [Theory]
    [InlineData("--help", 0)]
    [InlineData("-h", 0)]
    [InlineData("--unknown", 2)]
    public async Task HeadlessArgumentsDoNotRequireTerminal(string argument, int expected) {
        Type? example = typeof(CapabilityPlanningSampleTests).Assembly.GetType("Icod.Terminal.CapabilityPlanning.Sample.CapabilityPlanningExample");
        Assert.NotNull(example);
        MethodInfo? run = example.GetMethod("RunAsync", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(run);
        using StringWriter output = new();
        using StringWriter error = new();
        ValueTask<int> result = (ValueTask<int>)run.Invoke(null, [new[] {argument}, output, error, CancellationToken.None])!;
        Assert.Equal(expected, await result);
        Assert.Contains(expected == 0 ? "--verify" : "Unknown argument", expected == 0 ? output.ToString() : error.ToString());
    }
}