using System;

namespace Duccsoft;

public partial class Freecam
{
	/// <summary>
	/// Invoked whenever a freecam is enabled. The argument is the freecam that was enabled.
	/// </summary>
	public static event Action<Freecam> OnFreecamStart;
	/// <summary>
	/// Invoked whenever a freecam is disabled. The argument is the freecam that was disabled.
	/// </summary>
	public static event Action<Freecam> OnFreecamEnd;

	/// <summary>
	/// Removes all listeners from the static events of this class. Used by
	/// <see cref="CameraEventCleanupSystem"/> to tidy things up between play sessions.
	/// </summary>
	public static void ClearInvocationLists()
	{
		OnFreecamStart = null;
		OnFreecamEnd = null;
	}
}
