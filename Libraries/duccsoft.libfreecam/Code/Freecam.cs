namespace Duccsoft;

/// <summary>
/// Enables the use of AnalogMove and AnalogLook to control the position and rotation 
/// of the main camera of the scene.
/// </summary>
[Title( "Freecam" )]
[Category( "Camera" )]
[Icon( "control_camera" )]
public sealed partial class Freecam : Component
{
	/// <summary>
	/// How many units per second the camera will move at normal speed.
	/// </summary>
	[Property] public float Speed { get; set; } = 300f;
	/// <summary>
	/// A factor applied to movement speed whenever the crouch button is held.
	/// </summary>
	[Property] public float LowSpeedFactor { get; set; } = 0.25f;
	/// <summary>
	/// A factor applied to movement speed whenever the run button is held.
	/// </summary>
	[Property] public float HighSpeedFactor { get; set; } = 2.5f;
	/// <summary>
	/// If true, prevents the player from looking higher than directly up or lower
	/// than directly down. Prevents the camera from going upside-down and doing loop-de-loops.
	/// </summary>
	[Property] public bool ClampPitch { get; set; } = true;
	/// <summary>
	/// If true, the freecam will use a <see cref="CharacterController"/> to handle collisions.
	/// If none exists already, one will be created.
	/// </summary>
	[Property] public bool UseCollision { get; set; } = true;

	/// <summary>
	/// The main scene camera. Will be refreshed each update.
	/// </summary>
	private CameraComponent _camera;
	/// <summary>
	/// The current angle/rotation that we are looking at.
	/// </summary>
	private Angles _lookAngle;
	/// <summary>
	/// If <see cref="UseCollision"/> is true, this will be an instance of a <see cref="CharacterController"/>
	/// on the same GameObject as this component.
	/// </summary>
	private CharacterController _controller;

	protected override void OnEnabled()
	{
		OnFreecamStart?.Invoke( this );
	}

	protected override void OnDisabled()
	{
		OnFreecamEnd?.Invoke( this );
		_controller?.Destroy();
		_controller = null;
	}

	protected override void OnUpdate()
	{
		if ( _camera is null || !_camera.IsMainCamera )
		{
			_camera = Scene.Camera;
		}
		if ( !_camera.IsValid() )
			return;

		RotateMainCamera();
		MoveMainCamera();
	}

	protected override void OnFixedUpdate()
	{
		UpdatePosition();
	}

	private void RotateMainCamera()
	{
		_lookAngle += Input.AnalogLook;
		if ( ClampPitch )
		{
			_lookAngle.pitch = _lookAngle.pitch.Clamp( -89f, 89f );
		}
		_camera.Transform.Rotation = _lookAngle;
	}

	/// <summary>
	/// Move the main scene camera to roughly the position of this GameObject.
	/// </summary>
	private void MoveMainCamera()
	{
		if ( UseCollision )
		{
			// Put the camera up in to the center of the CharacterController's collision cube.
			_camera.Transform.Position = Transform.Position + Vector3.Up * 8f;
		}
		else
		{
			_camera.Transform.Position = Transform.Position;
		}
	}

	/// <summary>
	/// Use input to move this GameObject. If <see cref="UseCollision"/> is true, a <see cref="CharacterController"/>
	/// will be used to ensure that this GameObject doesn't clip through anything it shouldn't.
	/// </summary>
	private void UpdatePosition()
	{
		EnsureCollision();
		// Move using WASD or left thumbstick
		var movement = Input.AnalogMove * Speed * GetSpeedFactor();
		// Move relative to the direction the camera is facing.
		movement *= _lookAngle;
		if ( UseCollision )
		{
			_controller.Velocity = movement;
			_controller.Move();
			_controller.IsOnGround = false;
		}
		else
		{
			Transform.Position += movement * Time.Delta;
		}
	}

	private void EnsureCollision()
	{
		if ( !UseCollision )
			return;

		_controller ??= Components.GetOrCreate<CharacterController>();
		_controller.Radius = 8f;
		_controller.Height = 16f;
	}

	private float GetSpeedFactor()
	{
		var speedFactor = 1f;
		if ( Input.Down( CameraActions.MoveSlow ) )
		{
			speedFactor = LowSpeedFactor;
		}
		else if ( Input.Down( CameraActions.MoveFast ) )
		{
			speedFactor = HighSpeedFactor;
		}
		return speedFactor;
	}
}
