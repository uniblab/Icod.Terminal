/*
	Icod.Terminal.PackageCapabilityPlanningSmoke
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
using Icod.Terminal.CapabilityPlanning.Smoke;
using Icod.Terminal.CapabilityPlanning.Sample;

await CapabilityPlanningScenario.RunAsync( true );
await CapabilityPlanningScenario.RunAsync( false );
foreach ( string argument in new[] { "--help", "-h", "--invalid" } ) {
    using StringWriter output = new();
    using StringWriter error = new();
    int exit = await CapabilityPlanningExample.RunAsync( [ argument ], output, error );
    if ( exit != (argument == "--invalid" ? 2 : 0) ) throw new InvalidOperationException( "Headless CLI contract failed." );
}
Console.WriteLine( "Icod.Terminal 1.20 actual capability sample and verification matrix passed." );