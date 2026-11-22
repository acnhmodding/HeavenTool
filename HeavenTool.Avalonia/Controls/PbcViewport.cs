using Avalonia.Input;
using HeavenTool.Avalonia.FileTypes.PBC;
using HeavenTool.IO.FileFormats.PBC;
using HeavenTool.Rendering.PBC;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HeavenTool.Avalonia.Controls;

/// <summary>
/// 3D view of a PBC's surface. With the select tool a click picks the triangle under the cursor; the material and
/// height tools take the left button to edit, and a right drag orbits instead.
/// </summary>
public sealed class PbcViewport : GlViewport
{
    public PbcRenderer Renderer { get; }
    public PBCFileReader? File { get; set { field = value; EndStroke(); RefreshHover(); } }
    public PbcTool Tool { get; set { field = value; RefreshHover(); } }
    public TileType Brush { get; set; } = TileType.Grass;
    public int BrushSize { get; set { field = Math.Max(1, value); RefreshHover(); } } = 1;
    /// <summary>How far one raise or lower stroke moves each height.</summary>
    public float HeightStep { get; set; } = 1;
    /// <summary>Whether the height tools move whole triangles instead of the map points under the brush.</summary>
    public bool HeightFaces { get; set { field = value; RefreshHover(); } }

    public event Action<TilePosition>? TrianglePicked;
    public event Action<TilePosition?>? HoverChanged;
    /// <summary>Raised while a stroke edits, so the mesh can follow.</summary>
    public event Action? TilesPainted;
    /// <summary>Raised once per paint stroke or fill, after its changes are already applied to the file.</summary>
    public event Action<IReadOnlyList<TileChange>>? StrokeCompleted;
    /// <summary>Raised once per raise, lower or flatten stroke, after its changes are already applied to the file.</summary>
    public event Action<IReadOnlyList<HeightChange>>? HeightStrokeCompleted;

    private const float MarkerSize = .6f;
    private List<TileChange>? stroke;
    private HashSet<TilePosition>? strokeSeen;
    private List<HeightChange>? heightStroke;
    private HashSet<HeightPoint>? heightSeen;
    private float flattenHeight;
    private (float X, float Y)? cursor;

    public PbcViewport() : this(new PbcRenderer()) { }

    private PbcViewport(PbcRenderer renderer) : base(renderer, "PBC viewer") => Renderer = renderer;

    private bool IsHeightTool => Tool is PbcTool.Raise or PbcTool.Lower or PbcTool.Flatten;

    protected override bool RightDragOrbits => Tool != PbcTool.Select;

    private (TilePosition Position, Vector3 Point)? Hit(float x, float y)
    {
        if (File is not { } file || !Renderer.TryGetRay(x, y, out var origin, out var direction)) return null;
        if (PbcMesh.Pick(file, origin, direction) is not { } hit) return null;
        return (new TilePosition(hit.TileX, hit.TileZ, hit.Triangle), origin + direction * hit.Distance);
    }

    private List<TilePosition> Targets(PBCFileReader file, TilePosition position, Vector3 point) => Tool switch
    {
        PbcTool.Paint => PbcBrush.Footprint(file, position, point, BrushSize),
        PbcTool.Fill => PbcBrush.Fill(file, position),
        _ => [position]
    };

    private List<HeightPoint> HeightPoints(PBCFileReader file, TilePosition position, Vector3 point) => HeightFaces
        ? PbcBrush.FacePoints(file, position, point, BrushSize)
        : PbcBrush.Points(file, position, point, BrushSize);

    protected override void OnViewportClick(float x, float y, KeyModifiers modifiers)
    {
        if (Tool == PbcTool.Select && Hit(x, y) is { } hit) TrianglePicked?.Invoke(hit.Position);
    }

    protected override bool BeginTool(float x, float y, KeyModifiers modifiers)
    {
        if (Tool == PbcTool.Select || File is not { } file) return false;
        if (IsHeightTool)
        {
            // A flatten levels to the height under the press, kept for the whole stroke.
            if (Tool == PbcTool.Flatten)
            {
                if (Hit(x, y) is not { } hit) return false;
                flattenHeight = PbcBrush.NearestHeight(file, hit.Position, hit.Point);
            }
            heightStroke = [];
            heightSeen = [];
        }
        else
        {
            stroke = [];
            strokeSeen = [];
        }
        Apply(x, y);
        return true;
    }

    protected override void ContinueTool(float x, float y)
    {
        // A fill happens once, on the press.
        if (Tool == PbcTool.Fill) UpdateHover(x, y);
        else Apply(x, y);
    }

    protected override void EndTool()
    {
        var materials = stroke;
        var heights = heightStroke;
        EndStroke();
        if (materials is { Count: > 0 }) StrokeCompleted?.Invoke(materials);
        if (heights is { Count: > 0 }) HeightStrokeCompleted?.Invoke(heights);
    }

    private void EndStroke()
    {
        stroke = null;
        strokeSeen = null;
        heightStroke = null;
        heightSeen = null;
    }

    private void Apply(float x, float y)
    {
        cursor = (x, y);
        if (File is not { } file || Hit(x, y) is not { } hit)
        {
            ShowHover(null);
            return;
        }
        bool changed = false;
        if (stroke != null && strokeSeen != null)
        {
            foreach (var position in Targets(file, hit.Position, hit.Point))
            {
                if (!strokeSeen.Add(position)) continue;
                var materials = file[position.TileZ, position.TileX].Materials;
                var old = materials[(int)position.Triangle];
                if (old == Brush) continue;
                stroke.Add(new TileChange(position, old, Brush));
                materials[(int)position.Triangle] = Brush;
                changed = true;
            }
        }
        else if (heightStroke != null && heightSeen != null)
        {
            // Each height moves at most once per stroke, so dragging over an area raises it by one step, not by how long it took.
            foreach (var point in HeightPoints(file, hit.Position, hit.Point))
            {
                if (!heightSeen.Add(point)) continue;
                float old = HeightEditCommand.Get(file, point);
                float now = Tool switch
                {
                    PbcTool.Raise => old + HeightStep,
                    PbcTool.Lower => old - HeightStep,
                    _ => flattenHeight
                };
                if (old == now) continue;
                heightStroke.Add(new HeightChange(point, old, now));
                HeightEditCommand.Set(file, point, now);
                changed = true;
            }
        }
        ShowHover(hit);
        if (changed) TilesPainted?.Invoke();
    }

    protected override void OnViewportHover(float? x, float? y)
    {
        if (x is { } px && y is { } py) UpdateHover(px, py);
        else
        {
            cursor = null;
            ShowHover(null);
        }
    }

    private void UpdateHover(float x, float y)
    {
        cursor = (x, y);
        ShowHover(Hit(x, y));
    }

    /// <summary>Redraws the brush outline under a still cursor, e.g. after the file, the surface or the tool changed.</summary>
    public void RefreshHover()
    {
        if (cursor is { } at) UpdateHover(at.X, at.Y);
        else ShowHover(null);
    }

    private void ShowHover((TilePosition Position, Vector3 Point)? hit)
    {
        HoverChanged?.Invoke(hit?.Position);
        // The select tool needs no preview; the status bar names the triangle under the cursor.
        if (File is not { } file || hit is not { } at || Tool == PbcTool.Select) { Renderer.SetHover([]); return; }
        if (IsHeightTool && !HeightFaces) { Renderer.SetHover(Markers(file, PbcBrush.Points(file, at.Position, at.Point, BrushSize))); return; }
        var targets = IsHeightTool ? PbcBrush.Footprint(file, at.Position, at.Point, BrushSize) : Targets(file, at.Position, at.Point);
        Renderer.SetHover(PbcMesh.Outline(file, targets.Select(target => (target.TileX, target.TileZ, target.Triangle))));
    }

    /// <summary>A small cross at each distinct position the height brush moves.</summary>
    private static float[] Markers(PBCFileReader file, List<HeightPoint> points)
    {
        var seen = new HashSet<Vector3>();
        var lines = new List<float>();
        foreach (var point in points)
        {
            var (center, corner1, corner2) = PbcMesh.Corners(file, point.Position.TileX, point.Position.TileZ, point.Position.Triangle);
            var at = point.Corner switch { 0 => center, 1 => corner1, _ => corner2 };
            if (!seen.Add(at)) continue;
            foreach (var axis in (ReadOnlySpan<Vector3>)[Vector3.UnitX, Vector3.UnitZ, Vector3.UnitY])
            {
                var from = at - axis * MarkerSize;
                var to = at + axis * MarkerSize;
                lines.AddRange([from.X, from.Y, from.Z, to.X, to.Y, to.Z]);
            }
        }
        return [.. lines];
    }
}
