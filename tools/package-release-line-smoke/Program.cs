using System.Reflection;
using Icod.Terminal;

Type sessionType = typeof( TerminalSession );

PropertyInfo? rawInput = sessionType.GetProperty(
	"Input",
	BindingFlags.Instance | BindingFlags.Public
);
Require(
	rawInput is null,
	"The 1.0 package must not expose TerminalSession.Input."
);

PropertyInfo? rawOutput = sessionType.GetProperty(
	nameof( TerminalSession.Output ),
	BindingFlags.Instance | BindingFlags.Public
);
Require(
	rawOutput is not null,
	"The 1.0 package must retain the documented TerminalSession.Output advanced transport."
);

Require(
	typeof( ITerminalInput ).IsPublic,
	"ITerminalInput must remain public for custom transport injection."
);
Require(
	typeof( ITerminalOutput ).IsPublic,
	"ITerminalOutput must remain public for custom transport injection."
);
Require(
	typeof( ITerminalControlProvider ).IsPublic,
	"ITerminalControlProvider must remain public for custom platform providers."
);

HashSet<string> publicMethods = sessionType
	.GetMethods(
		BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public
	)
	.Select( static method => method.Name )
	.ToHashSet( StringComparer.Ordinal );

string[] requiredMethods = [
	nameof( TerminalSession.OpenAsync ),
	nameof( TerminalSession.ReadEventAsync ),
	nameof( TerminalSession.WriteTextAsync ),
	nameof( TerminalSession.QueryCursorPositionAsync ),
	nameof( TerminalSession.AcquireInputProtocolsAsync ),
	nameof( TerminalSession.AcquirePresentationAsync ),
	nameof( TerminalSession.AcquireCursorStyleAsync ),
	nameof( TerminalSession.AcquireSynchronizedOutputAsync ),
	nameof( TerminalSession.AcquireProgressAsync ),
	nameof( TerminalSession.AcquirePointerShapeAsync ),
	nameof( TerminalSession.AcquirePaletteColorAsync ),
	nameof( TerminalSession.AcquireDynamicColorAsync ),
	nameof( TerminalSession.BeginPromptAsync ),
	nameof( TerminalSession.SendNotificationAsync ),
	nameof( TerminalSession.PublishCurrentLocationAsync ),
	nameof( TerminalSession.ReadClipboardAsync )
];

foreach ( string methodName in requiredMethods ) {
	Require(
		publicMethods.Contains( methodName ),
		$"The 1.0 package is missing required TerminalSession operation '{methodName}'."
	);
}

Require(
	0 == Convert.ToInt32( TerminalInputEventKind.Text ),
	"TerminalInputEventKind.Text must remain 0."
);
Require(
	5 == Convert.ToInt32( TerminalInputEventKind.EndOfInput ),
	"TerminalInputEventKind.EndOfInput must remain 5."
);
Require(
	3 == Convert.ToInt32( TerminalEventKind.Cancelled ),
	"TerminalEventKind.Cancelled must remain 3."
);
Require(
	2 == Convert.ToInt32( TerminalKeyEventPhase.Release ),
	"TerminalKeyEventPhase.Release must remain 2."
);
Require(
	2 == Convert.ToInt32( TerminalKeyboardReportingMode.AllKeys ),
	"TerminalKeyboardReportingMode.AllKeys must remain 2."
);
Require(
	128 == Convert.ToInt32( TerminalKeyModifiers.NumLock ),
	"TerminalKeyModifiers.NumLock must remain 128."
);

_ = typeof( TerminalInputProtocolLease );
_ = typeof( TerminalPresentationLease );
_ = typeof( TerminalSynchronizedOutputLease );
_ = typeof( TerminalProgressLease );
_ = typeof( TerminalPointerShapeLease );
_ = typeof( TerminalPaletteColorLease );
_ = typeof( TerminalDynamicColorLease );
_ = typeof( TerminalCursorStyleLease );
_ = typeof( TerminalColor );

Version? assemblyVersion = sessionType.Assembly.GetName().Version;
Require(
	assemblyVersion is not null && 1 == assemblyVersion.Major,
	"The stable 1.x package must expose a 1.x assembly version."
);

Console.WriteLine(
	"Icod.Terminal stable 1.x release-line package contract smoke passed."
);

static void Require(
	bool condition,
	string message
) {
	ArgumentNullException.ThrowIfNull( message );
	if ( !condition ) {
		throw new InvalidOperationException( message );
	}
}
