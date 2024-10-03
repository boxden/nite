namespace Duccsoft;

/// <summary>
/// On game shutdown, clears the invocation lists of the static events of <see cref="Freecam"/>.
/// This is to prevent duplicate event subscribers from piling up between play sessions.
/// </summary>
public class CameraEventCleanupSystem : GameObjectSystem
{
	public CameraEventCleanupSystem( Scene scene ) : base( scene ) 
	{ 
	
	} 

	public override void Dispose()
	{
		base.Dispose();

		Freecam.ClearInvocationLists();
	}
}
