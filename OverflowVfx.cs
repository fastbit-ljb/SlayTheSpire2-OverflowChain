using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace OverflowChainMod;

/// <summary>
/// Custom electric-shock vfx layered on top of the vanilla lightning strike:
/// a brightened blue tint on each chained monster's body, an additive glow
/// behind it, small arcs crawling on the body, and jagged bolts linking
/// consecutive chain hops. Everything is built from code (runtime Shader,
/// GradientTexture2D, Line2D), so the pck needs no new assets. Materials and
/// nodes are created directly instead of going through PreloadManager.Cache,
/// which disposes non-preloaded (mod) resources on scene changes.
/// </summary>
public static class OverflowVfx
{
    public const string EvokeSfx = "event:/sfx/characters/defect/defect_lightning_evoke";

    private const float FlickerTime = 0.3f;   // seconds arcs keep re-jittering
    private const float JitterInterval = 0.06f;
    private const float FadeTime = 0.4f;
    private const float BodyGlowTime = 1.1f;  // seconds the body stays lit

    private static readonly Color CoreColor = new(0.93f, 0.98f, 1f);
    private static readonly Color GlowColor = new(0.45f, 0.72f, 1f);

    private static Shader? _bodyShader;

    private const string BodyShaderCode = """
        shader_type canvas_item;

        uniform float strength : hint_range(0.0, 4.0) = 1.0;
        uniform vec4 tint : source_color = vec4(0.55, 0.78, 1.0, 1.0);

        void fragment() {
            vec4 col = texture(TEXTURE, UV);
            float lum = dot(col.rgb, vec3(0.299, 0.587, 0.114));
            float pulse = 0.85 + 0.15 * sin(TIME * 17.0);
            vec3 lit = col.rgb * (1.0 + strength * 0.55) + tint.rgb * lum * strength * 0.5;
            COLOR = vec4(lit * pulse, col.a);
        }
        """;

    /// <summary>Screen position of a creature's vfx center, if its node exists.</summary>
    public static Vector2? GetCenter(Creature creature)
    {
        return creature.GetCreatureNode()?.VfxSpawnPosition;
    }

    /// <summary>Light up one chained monster: body tint, back glow, body arcs.</summary>
    public static void PlayHop(Creature target)
    {
        NCreature? node = target.GetCreatureNode();
        if (node == null)
        {
            return;
        }
        ApplyBodyGlow(node);
        SpawnAura(node);
        SpawnBodyArcs(node);
    }

    /// <summary>Draw a flickering bolt between two arena-space points.</summary>
    public static void PlayBolt(Vector2 from, Vector2 to)
    {
        Control? container = NCombatRoom.Instance?.CombatVfxContainer;
        if (container == null)
        {
            return;
        }
        Vector2 a = ToLocalSpace(container, from);
        Vector2 b = ToLocalSpace(container, to);
        float length = a.DistanceTo(b);
        if (length < 1f)
        {
            return;
        }
        var bolt = new Node2D();
        container.AddChildSafely(bolt);
        Line2D glow = NewLine(14f, new Color(GlowColor.R, GlowColor.G, GlowColor.B, 0.4f));
        Line2D core = NewLine(4.5f, CoreColor);
        Line2D branch = NewLine(2.5f, new Color(CoreColor.R, CoreColor.G, CoreColor.B, 0.8f));
        bolt.AddChildSafely(glow);
        bolt.AddChildSafely(core);
        bolt.AddChildSafely(branch);
        float amplitude = Math.Min(length * 0.16f, 46f);
        TaskHelper.RunSafely(RunBoltAsync(bolt, glow, core, branch, a, b, amplitude));
    }

    private static void ApplyBodyGlow(NCreature node)
    {
        MegaSprite? body = node.Visuals.SpineBody;
        Node2D? bodyNode = node.Visuals.Body;
        if (body == null || bodyNode == null)
        {
            return;
        }
        Material? saved = body.GetNormalMaterial();
        _bodyShader ??= new Shader { Code = BodyShaderCode };
        var material = new ShaderMaterial { Shader = _bodyShader };
        material.SetShaderParameter("strength", 1.0f);
        body.SetNormalMaterial(material);
        Tween tween = bodyNode.CreateTween();
        tween.TweenInterval(BodyGlowTime);
        tween.TweenCallback(Callable.From(() =>
        {
            // MegaSprite is a plain wrapper, so validity goes through its node.
            if (GodotObject.IsInstanceValid(bodyNode))
            {
                body.SetNormalMaterial(saved!);
            }
        }));
    }

    private static void SpawnAura(NCreature node)
    {
        Control? container = NCombatRoom.Instance?.BackCombatVfxContainer;
        if (container == null)
        {
            return;
        }
        float diameter = Math.Max(node.Hitbox.Size.X, node.Hitbox.Size.Y) * 1.6f;
        if (diameter <= 0f)
        {
            diameter = 240f;
        }
        var gradient = new Gradient
        {
            Offsets = new[] { 0f, 0.55f, 1f },
            Colors = new[]
            {
                new Color(GlowColor.R, GlowColor.G, GlowColor.B, 0.5f),
                new Color(GlowColor.R, GlowColor.G, GlowColor.B, 0.22f),
                new Color(GlowColor.R, GlowColor.G, GlowColor.B, 0f),
            },
        };
        var sprite = new Sprite2D
        {
            Texture = new GradientTexture2D
            {
                Gradient = gradient,
                Fill = GradientTexture2D.FillEnum.Radial,
                FillFrom = new Vector2(0.5f, 0.5f),
                FillTo = new Vector2(0.5f, 0f),
                Width = 256,
                Height = 256,
            },
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
            Scale = Vector2.One * (diameter / 256f),
            Modulate = new Color(1f, 1f, 1f, 0f),
        };
        container.AddChildSafely(sprite);
        sprite.GlobalPosition = node.VfxSpawnPosition;
        Tween tween = sprite.CreateTween();
        tween.TweenProperty(sprite, "modulate:a", 0.8f, 0.1f);
        tween.TweenProperty(sprite, "modulate:a", 0.5f, 0.3f);
        tween.TweenInterval(0.25f);
        tween.TweenProperty(sprite, "modulate:a", 0f, FadeTime);
        tween.TweenCallback(Callable.From(() => sprite.QueueFreeSafely()));
    }

    private static void SpawnBodyArcs(NCreature node)
    {
        Vector2 center = ToLocalSpace(node, node.VfxSpawnPosition);
        Vector2 half = node.Hitbox.Size * 0.5f;
        if (half.X <= 0f || half.Y <= 0f)
        {
            half = new Vector2(80f, 60f);
        }
        int count = 2 + (int)(GD.Randf() * 2f);
        for (int i = 0; i < count; i++)
        {
            bool isCore = i == 0;
            Line2D line = NewLine(
                isCore ? 3.5f : 8f,
                isCore ? CoreColor : new Color(GlowColor.R, GlowColor.G, GlowColor.B, 0.55f));
            node.AddChildSafely(line);
            Vector2 start = center
                + new Vector2(Rand(-half.X, half.X) * 0.6f, Rand(-half.Y, half.Y) * 0.6f);
            Vector2 direction = new Vector2(Rand(-1f, 1f), Rand(-1f, 1f)).Normalized();
            Vector2 end = start + direction * Rand(50f, 110f);
            TaskHelper.RunSafely(RunJaggedAsync(line, () => Jagged(start, end, start.DistanceTo(end) * 0.28f)));
        }
    }

    private static async Task RunBoltAsync(Node2D bolt, Line2D glow, Line2D core, Line2D branch, Vector2 a, Vector2 b, float amplitude)
    {
        double elapsed = await Flicker(bolt, () =>
        {
            Vector2[] points = Jagged(a, b, amplitude);
            glow.Points = points;
            core.Points = points;
            branch.Points = BranchPoints(points, a, b);
        });
        if (elapsed < 0d || !GodotObject.IsInstanceValid(bolt))
        {
            return;
        }
        Tween tween = bolt.CreateTween();
        tween.TweenProperty(bolt, "modulate:a", 0f, FadeTime);
        tween.TweenCallback(Callable.From(() => bolt.QueueFreeSafely()));
    }

    private static async Task RunJaggedAsync(Line2D line, Func<Vector2[]> regenerate)
    {
        double elapsed = await Flicker(line, () => line.Points = regenerate());
        if (elapsed < 0d || !GodotObject.IsInstanceValid(line))
        {
            return;
        }
        Tween tween = line.CreateTween();
        tween.TweenProperty(line, "modulate:a", 0f, FadeTime);
        tween.TweenCallback(Callable.From(() => line.QueueFreeSafely()));
    }

    /// <summary>Re-jitter the jagged shape for a while. Returns elapsed seconds,
    /// or -1 if the host node died mid-flicker.</summary>
    private static async Task<double> Flicker(Node host, Action refresh)
    {
        double elapsed = 0d;
        while (elapsed < FlickerTime)
        {
            if (!GodotObject.IsInstanceValid(host))
            {
                return -1d;
            }
            refresh();
            SceneTree? tree = host.GetTree();
            if (tree == null)
            {
                return -1d;
            }
            await tree.ToSignal(tree.CreateTimer(JitterInterval), SceneTreeTimer.SignalName.Timeout);
            if (!GodotObject.IsInstanceValid(host))
            {
                return -1d;
            }
            elapsed += JitterInterval;
        }
        return elapsed;
    }

    /// <summary>Lightning shape between two points via midpoint displacement.</summary>
    private static Vector2[] Jagged(Vector2 a, Vector2 b, float amplitude)
    {
        var points = new List<Vector2> { a, b };
        float amp = amplitude;
        for (int iteration = 0; iteration < 3; iteration++)
        {
            var next = new List<Vector2>(points.Count * 2);
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector2 p0 = points[i];
                Vector2 p1 = points[i + 1];
                next.Add(p0);
                Vector2 dir = (p1 - p0).Normalized();
                Vector2 mid = (p0 + p1) * 0.5f + new Vector2(-dir.Y, dir.X) * (Rand(-amp, amp));
                next.Add(mid);
            }
            next.Add(points[^1]);
            points = next;
            amp *= 0.5f;
        }
        return points.ToArray();
    }

    private static Vector2[] BranchPoints(Vector2[] mainPoints, Vector2 a, Vector2 b)
    {
        if (mainPoints.Length < 5)
        {
            return Array.Empty<Vector2>();
        }
        int index = GD.RandRange(2, mainPoints.Length - 3);
        Vector2 start = mainPoints[index];
        Vector2 dir = (b - a).Normalized();
        Vector2 side = new Vector2(-dir.Y, dir.X);
        Vector2 end = start + dir * Rand(18f, 42f) + side * Rand(-45f, 45f);
        return new[] { start, (start + end) * 0.5f + side * Rand(-10f, 10f), end };
    }

    /// <summary>Controls lack ToLocal; go through the canvas transform.</summary>
    private static Vector2 ToLocalSpace(CanvasItem item, Vector2 globalPoint)
    {
        return item.GetGlobalTransform().AffineInverse() * globalPoint;
    }

    private static Line2D NewLine(float width, Color color)
    {
        return new Line2D
        {
            Width = width,
            DefaultColor = color,
            JointMode = Line2D.LineJointMode.Round,
            BeginCapMode = Line2D.LineCapMode.Round,
            EndCapMode = Line2D.LineCapMode.Round,
            Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add },
        };
    }

    private static float Rand(float min, float max)
    {
        return min + (max - min) * GD.Randf();
    }
}
