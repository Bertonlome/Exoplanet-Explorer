using System;
using System.Collections.Generic;
using Godot;

namespace Game.UI.Tutorial;

public sealed class TutorialTargetRegistration : IDisposable
{
	private TutorialTargetRegistry registry;
	private readonly string targetId;
	private readonly GodotObject owner;

	internal TutorialTargetRegistration(
		TutorialTargetRegistry registry,
		string targetId,
		GodotObject owner)
	{
		this.registry = registry;
		this.targetId = targetId;
		this.owner = owner;
	}

	public void Dispose()
	{
		registry?.Unregister(targetId, owner);
		registry = null;
	}
}

public partial class TutorialTargetRegistry : Node
{
	private sealed class Entry
	{
		public GodotObject Owner { get; }
		public Node TargetNode { get; }
		public Func<Rect2?> ScreenRectProvider { get; }

		public Entry(GodotObject owner, Node targetNode, Func<Rect2?> screenRectProvider)
		{
			Owner = owner;
			TargetNode = targetNode;
			ScreenRectProvider = screenRectProvider;
		}
	}

	private readonly Dictionary<string, Entry> entries = new();

	public TutorialTargetRegistration RegisterControl(string targetId, Control control)
	{
		if (control == null)
		{
			throw new ArgumentNullException(nameof(control));
		}
		return Register(
			targetId,
			control,
			control,
			() => IsInstanceValid(control) && control.IsVisibleInTree()
				? control.GetGlobalRect()
				: null);
	}

	public TutorialTargetRegistration RegisterRectProvider(
		string targetId,
		GodotObject owner,
		Func<Rect2?> screenRectProvider,
		Node targetNode = null)
	{
		return Register(targetId, owner, targetNode, screenRectProvider);
	}

	public bool TryResolve(string targetId, out Node targetNode, out Rect2 screenRect)
	{
		targetNode = null;
		screenRect = new Rect2();
		if (string.IsNullOrWhiteSpace(targetId) || !entries.TryGetValue(targetId, out Entry entry))
		{
			return false;
		}

		if (!IsInstanceValid(entry.Owner))
		{
			entries.Remove(targetId);
			return false;
		}

		Rect2? resolvedRect = entry.ScreenRectProvider?.Invoke();
		if (!resolvedRect.HasValue || resolvedRect.Value.Size.X <= 0f || resolvedRect.Value.Size.Y <= 0f)
		{
			return false;
		}

		targetNode = IsInstanceValid(entry.TargetNode) ? entry.TargetNode : null;
		screenRect = resolvedRect.Value;
		return true;
	}

	public void Unregister(string targetId, GodotObject owner)
	{
		if (entries.TryGetValue(targetId, out Entry entry) && entry.Owner == owner)
		{
			entries.Remove(targetId);
		}
	}

	public override void _ExitTree()
	{
		entries.Clear();
	}

	private TutorialTargetRegistration Register(
		string targetId,
		GodotObject owner,
		Node targetNode,
		Func<Rect2?> screenRectProvider)
	{
		if (string.IsNullOrWhiteSpace(targetId))
		{
			throw new ArgumentException("A tutorial target requires a non-empty ID.", nameof(targetId));
		}
		if (owner == null)
		{
			throw new ArgumentNullException(nameof(owner));
		}
		if (screenRectProvider == null)
		{
			throw new ArgumentNullException(nameof(screenRectProvider));
		}

		if (entries.ContainsKey(targetId))
		{
			GD.PushWarning($"Tutorial target '{targetId}' was replaced by a newer registration.");
		}
		entries[targetId] = new Entry(owner, targetNode, screenRectProvider);
		return new TutorialTargetRegistration(this, targetId, owner);
	}
}
