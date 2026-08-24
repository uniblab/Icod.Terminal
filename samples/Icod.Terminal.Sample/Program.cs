using System.Reflection;

Assembly assembly = Assembly.Load("Icod.Terminal");
Type? marker = assembly.GetType(
	"Icod.Terminal.AssemblyMarker",
	throwOnError: false);

if (marker is null) {
	Console.Error.WriteLine("Icod.Terminal foundation marker was not found.");
	return 1;
}

Console.WriteLine("Icod.Terminal T01 foundation loaded.");
return 0;
