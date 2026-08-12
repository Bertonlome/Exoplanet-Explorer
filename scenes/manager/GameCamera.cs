using System;
using Game.Autoload;
using Game.Component;
using Game.Manager;
using Godot;

namespace Game;

public partial class GameCamera : Camera2D
{
	private const int TILE_SIZE = 64;
	private const float PAN_SPEED = 1000;
	private const float NOISE_SAMPLE_GROWTH = .1f;
	private const float MAX_CAMERA_OFFSET = 24;
	private const float NOISE_FREQUENCY_MULTIPLIER = 100;
	private const float SHAKE_DECAY = 3;

	private readonly StringName ACTION_PAN_LEFT = "pan_left";
	private readonly StringName ACTION_PAN_RIGHT = "pan_right";
	private readonly StringName ACTION_PAN_UP = "pan_up";
	private readonly StringName ACTION_PAN_DOWN = "pan_down";
	private readonly StringName ACTION_SPACEBAR = "spacebar";

	private readonly StringName ACTION_SCROLL_FORWARD = "scroll_forward";
	private readonly StringName ACTION_SCROLL_BACKWARD = "scroll_backward";
	private readonly StringName ACTION_UNZOOM = "unzoom";

	private enum State
	{
		CameraFree,
		TrackingRobot,
	}

	[Export]
	private FastNoiseLite shakeNoise;
	[Export]
	private BuildingManager buildingManager;
	[Export]
	private float zoomStep = 0.1f;
	[Export]
	private float minZoom = 0.3f;
	[Export]
	private float maxZoom = 2.5f;
	[Signal]
	public delegate void CameraZoomEventHandler();

	private static GameCamera instance;
	private State currentState {get; set; }= State.CameraFree;

	private Vector2 noiseSample;
	private float currentShakePercentage;
	private BuildingComponent robotTracked;
	
	// Mouse drag variables
	private bool isDragging = false;
	private Vector2 lastMousePosition;

    public override void _Ready()
    {
		SettingManager.Instance.Connect(SettingManager.SignalName.TrackingRobot, Callable.From<BuildingComponent>(OnTrackingRobot));
		SettingManager.Instance.Connect(SettingManager.SignalName.StopTrackingRobot, Callable.From(OnStopTrackingRobot));
    }

    public static void Shake()
	{
		instance.currentShakePercentage = 1;
	}

    public override void _Notification(int what)
    {
        if(what == NotificationSceneInstantiated)
		{
			instance = this;
		}
    }

    public override void _Process(double delta)
	{
		// Handle mouse drag panning
		if (isDragging && currentState == State.CameraFree)
		{
			var currentMousePosition = GetViewport().GetMousePosition();
			var mouseDelta = lastMousePosition - currentMousePosition;
			// Scale mouse delta by zoom to maintain consistent drag speed
			GlobalPosition += mouseDelta / Zoom;
			lastMousePosition = currentMousePosition;
			
			// Apply camera limits
			var viewPortrect = GetViewportRect();
			var halfWidth = viewPortrect.Size.X / 2;
			var halfHeight = viewPortrect.Size.Y / 2;
			var xClamped = Mathf.Clamp(GlobalPosition.X, LimitLeft + halfWidth, LimitRight - halfWidth);
			var yClamped = Mathf.Clamp(GlobalPosition.Y, LimitTop + halfHeight, LimitBottom - halfHeight);
			GlobalPosition = new Vector2(xClamped, yClamped);
		}
		
		// Smooth dezoom while U is held
		if (Input.IsActionPressed(ACTION_UNZOOM) && Zoom.X > minZoom)
		{
			var newZoom = Mathf.Max(Zoom.X - zoomStep, minZoom);
			Zoom = new Vector2(newZoom, newZoom);
		}

		switch (currentState)
		{
			case State.CameraFree:
				var movementVector = Input.GetVector(ACTION_PAN_LEFT, ACTION_PAN_RIGHT, ACTION_PAN_UP, ACTION_PAN_DOWN);
				GlobalPosition += movementVector * PAN_SPEED * (float)delta;

				var viewPortrect = GetViewportRect();
				var halfWidth = viewPortrect.Size.X / 2;
				var halfHeight = viewPortrect.Size.Y /2;
				var xClamped = Mathf.Clamp(GlobalPosition.X, LimitLeft + halfWidth, LimitRight - halfWidth);
				var yClamped = Mathf.Clamp(GlobalPosition.Y, LimitTop + halfHeight, LimitBottom - halfHeight);
				GlobalPosition = new Vector2(xClamped, yClamped);
				ApplyCameraShake(delta);
			break;

			case State.TrackingRobot:
				if (robotTracked == null) 
				{
					ChangeState(State.CameraFree);
					break;
				}
				CenterOnPosition(robotTracked.GlobalPosition);
				break;
		}

	}

	public override void _UnhandledInput(InputEvent evt)
	{
		// Handle mouse button press/release for drag panning
		if (evt is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Left)
			{
				if (mouseButton.Pressed)
				{
					// Don't start dragging if BuildingManager is in painting mode
					if (buildingManager.IsInPaintingMode)
					{
						return;
					}

					if (currentState == State.TrackingRobot)
					{
						OnStopTrackingRobot();
					}

					if (currentState == State.CameraFree)
					{
						// Start dragging - input reached here so BuildingManager didn't handle it
						isDragging = true;
						lastMousePosition = GetViewport().GetMousePosition();
					}
				}
				else if (!mouseButton.Pressed && isDragging)
				{
					// Stop dragging
					isDragging = false;
				}
			}
		}
		
		if(evt.IsActionPressed(ACTION_SPACEBAR) && currentState == State.CameraFree)
		{
			CenterOnPosition(buildingManager.hoveredGridArea.Position * 64);
			GetViewport().SetInputAsHandled();
		}
		if (evt.IsActionPressed(ACTION_SCROLL_FORWARD) && Zoom.X <= maxZoom)
		{
			Zoom = new Vector2(Zoom.X + zoomStep, Zoom.Y + zoomStep);
			//EmitSignal(SignalName.CameraZoom);
		}
		if (evt.IsActionPressed(ACTION_SCROLL_BACKWARD) && Zoom.X >= minZoom)
		{
			Zoom = new Vector2(Zoom.X - zoomStep, Zoom.Y - zoomStep);
			//EmitSignal(SignalName.CameraZoom);
		}
	}

	public void SetBoundingRect(Rect2I boundingRect)
	{
		LimitLeft = boundingRect.Position.X * TILE_SIZE;
		LimitRight = boundingRect.End.X * TILE_SIZE;
		LimitTop = boundingRect.Position.Y * TILE_SIZE;
		LimitBottom = boundingRect.End.Y * TILE_SIZE;
	}

	public void CenterOnPosition(Vector2 position)
	{
		GlobalPosition = position;
	}

	public void CancelMouseDrag()
	{
		isDragging = false;
	}

	public void CenterOnPositionClamped(Vector2 position)
	{
		OnStopTrackingRobot();
		GlobalPosition = ClampCenterToWorld(position);
	}

	public Rect2 GetVisibleWorldRect()
	{
		var viewportSize = GetViewportRect().Size;
		var visibleSize = new Vector2(
			viewportSize.X / MathF.Max(Zoom.X, 0.001f),
			viewportSize.Y / MathF.Max(Zoom.Y, 0.001f));
		return new Rect2(GlobalPosition - visibleSize * 0.5f, visibleSize);
	}

	public Rect2 GetWorldBounds()
	{
		return new Rect2(
			new Vector2(LimitLeft, LimitTop),
			new Vector2(LimitRight - LimitLeft, LimitBottom - LimitTop));
	}

	public void SetCameraZoomStep(float factor)
	{
		if (factor <= 0f) return;

		float newZoom = Mathf.Clamp(Zoom.X * factor, minZoom, maxZoom);
		Zoom = Vector2.One * newZoom;
		GlobalPosition = ClampCenterToWorld(GlobalPosition);
		EmitSignal(SignalName.CameraZoom);
	}

	private Vector2 ClampCenterToWorld(Vector2 position)
	{
		var visibleSize = GetVisibleWorldRect().Size;
		return new Vector2(
			ClampCameraAxis(position.X, LimitLeft, LimitRight, visibleSize.X * 0.5f),
			ClampCameraAxis(position.Y, LimitTop, LimitBottom, visibleSize.Y * 0.5f));
	}

	private static float ClampCameraAxis(float value, float lowerLimit, float upperLimit, float halfViewSize)
	{
		float minimum = lowerLimit + halfViewSize;
		float maximum = upperLimit - halfViewSize;
		if (minimum > maximum)
		{
			return (lowerLimit + upperLimit) * 0.5f;
		}
		return Mathf.Clamp(value, minimum, maximum);
	}

	private void ApplyCameraShake(double delta)
	{
		if (currentShakePercentage > 0)
		{
			noiseSample.X += NOISE_SAMPLE_GROWTH * NOISE_FREQUENCY_MULTIPLIER * (float)delta;
			noiseSample.Y += NOISE_SAMPLE_GROWTH * NOISE_FREQUENCY_MULTIPLIER *(float)delta;

			currentShakePercentage = Mathf.Clamp(currentShakePercentage - (SHAKE_DECAY * (float)delta), 0, 1);
		}
		var xSample = shakeNoise.GetNoise2D(noiseSample.X, 0);
		var ySample = shakeNoise.GetNoise2D(0, noiseSample.Y);

		Offset = new Vector2(MAX_CAMERA_OFFSET * xSample, MAX_CAMERA_OFFSET * ySample) * currentShakePercentage;
	}

	private void ChangeState(State toState)
	{
		currentState = toState;
	}

	private void OnTrackingRobot(BuildingComponent buildingComponent)
	{
		ChangeState(State.TrackingRobot);
		robotTracked = buildingComponent;
	}

		private void OnStopTrackingRobot()
	{
		ChangeState(State.CameraFree);
		robotTracked = null;
	}
}
