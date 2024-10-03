namespace Duccsoft;

public sealed partial class Freecam
{
	/// <summary>
	/// Holds whatever instance of <see cref="Freecam"/> may have been created by invoking <see cref="Toggle"/>.
	/// </summary>
	private static Freecam _conCmdInstance;
	/// <summary>
	/// Holds references to whatever <see cref="Freecam"/> instances were disabled by <see cref="Toggle"/>.
	/// </summary>
	private static HashSet<Freecam> _disabledFreecams = new();

	[ConCmd("freecam", Help = "Toggle a mode where the camera can be moved and rotated freely.")]
	public static void Toggle()
	{
		var camera = Game.ActiveScene?.Camera;
		if ( !camera.IsValid() )
			return;

		// If we were already using the freecam ConCmd...
		if ( _conCmdInstance?.Active == true )
		{
			// ...toggle it off by destroying its GameObject.
			_conCmdInstance?.GameObject?.Destroy();
			_conCmdInstance = null;
			// Reenable whatever non-ConCmd freecam we might have previously disabled.
			ReenablePreviousFreecam();
			return;
		}

		// If there is an active freecam not created by this ConCmd, disable it.
		DisableActiveFreecams();
		// Make a new GameObject with a Freecam component.
		CreateFreecam();
	}

	private static void DisableActiveFreecams()
	{
		// Find any freecams that are currently active...
		var freecams = Game.ActiveScene.GetAllComponents<Freecam>();
		if ( !freecams.Any() )
			return;

		foreach ( var freecam in freecams )
		{
			// ...and turn them off.
			freecam.Enabled = false;
			_disabledFreecams.Add( freecam );
		}
		return;
	}

	private static void ReenablePreviousFreecam()
	{
		// Clean out any invalid freecams (e.g. GameObject or component was deleted)
		foreach( var freecam in _disabledFreecams.ToList() )
		{
			if ( !freecam.IsValid() )
			{
				_disabledFreecams.Remove( freecam );
			}
		}
		// Find the first valid freecam we had previously disabled
		var firstFreecam = _disabledFreecams.FirstOrDefault();
		if ( firstFreecam is null )
			return;

		firstFreecam.Enabled = true;
		_disabledFreecams.Remove( firstFreecam );
	}

	private static void CreateFreecam()
	{
		// There were no active freecams, so create one.
		var freecamGo = new GameObject( true, "ConCmd Freecam" );
		var camTx = Game.ActiveScene.Camera.Transform;
		freecamGo.Transform.Position = camTx.Position;
		_conCmdInstance = freecamGo.Components.Create<Freecam>();
		_conCmdInstance._lookAngle = camTx.Rotation;
		// Assume that if someone uses a ConCmd to freecam, they also want to noclip.
		// Colliding with walls is more of an official "photo mode" or "spectator mode" kind of thing.
		_conCmdInstance.UseCollision = false;
	}
}
