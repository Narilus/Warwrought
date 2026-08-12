using System;
using Godot;
using Warwrought.Battle.Model;

namespace Warwrought.Presentation.Battle;

/// <summary>
/// Maintained BattleLab terrain/decor root. The scene provides the required roots; this production
/// node consumes the selected M2.1 profile, builds the visible ArrayMesh, and scatters static
/// presentation placeholders without collision or per-node update scripts.
/// </summary>
public partial class BattlefieldTerrainView : Node3D
{
    public const string SideATexturePath = "res://assets/sprites/m1_side_a_infantry.tres";
    public const string SideBTexturePath = "res://assets/sprites/m1_side_b_infantry.tres";

    private BattlefieldPresentationProjector? _projector;
    private BattlefieldTerrainPresenter? _presenter;
    private Node3D? _foliageRoot;
    private Node3D? _propsRoot;
    private MeshInstance3D? _ground;
    private MeshInstance3D? _contactLine;
    private Texture2D? _treeTexture;
    private Texture2D? _bushTexture;

    public bool IsConfigured => _presenter is not null;

    public BattlefieldDefinition Definition => _presenter?.Definition ?? throw new InvalidOperationException("BattlefieldTerrainView has not been configured.");

    public BattlefieldTerrainMeshData MeshData => _presenter?.MeshData ?? throw new InvalidOperationException("BattlefieldTerrainView has not been configured.");

    public int MeshTriangleCount => _presenter?.TriangleCount ?? 0;

    public int MeshVertexCount => _presenter?.VertexCount ?? 0;

    public bool MeshSamplerAgreement => _presenter?.MeshData.HasSamplerAgreement() ?? false;

    public int FoliagePlacementCount { get; private set; }

    public int PropCount { get; private set; }

    public string FoliageDigest { get; private set; } = string.Empty;

    public override void _Ready()
    {
        ValidateSceneStructure();
    }

    public void Configure(BattlefieldPresentationProjector projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        ValidateSceneStructure();
        if (IsConfigured)
        {
            throw new InvalidOperationException("BattlefieldTerrainView cannot be configured twice.");
        }

        _projector = projector;
        _presenter = new BattlefieldTerrainPresenter(projector);
        _ground!.Mesh = _presenter.BuildMesh();
        _ground.MaterialOverride = BattlefieldTerrainPresenter.CreateMaterial();
        PositionContactLine();
        LoadRequiredDecorationTextures();
        CreateDecorations();
    }

    private void ValidateSceneStructure()
    {
        _ground ??= GetNode<MeshInstance3D>("Ground");
        _contactLine ??= GetNode<MeshInstance3D>("ContactLine");
        _foliageRoot ??= GetNode<Node3D>("FoliageRoot");
        _propsRoot ??= GetNode<Node3D>("PropsRoot");
    }

    private void PositionContactLine()
    {
        var center = Definition.Bounds.Center;
        _contactLine!.Position = _projector!.Project(
            center,
            BattlefieldPresentationProjector.ContactMarkerVerticalOffset,
            BattlefieldProjectionPurpose.ContactMarker);
    }

    private void LoadRequiredDecorationTextures()
    {
        _treeTexture = GD.Load<Texture2D>(SideATexturePath)
            ?? throw new InvalidOperationException($"BattleLab required foliage placeholder resource is missing: {SideATexturePath}");
        _bushTexture = GD.Load<Texture2D>(SideBTexturePath)
            ?? throw new InvalidOperationException($"BattleLab required foliage placeholder resource is missing: {SideBTexturePath}");
    }

    private void CreateDecorations()
    {
        var scatter = new BattlefieldFoliageScatter(Definition);
        var placements = scatter.CreatePlacements();
        FoliagePlacementCount = placements.Count;
        FoliageDigest = scatter.ComputePlacementDigest(placements);

        for (var index = 0; index < placements.Count; index++)
        {
            var placement = placements[index];
            if (placement.Kind == BattlefieldDecorationKind.Rock)
            {
                CreateRock(placement);
            }
            else
            {
                CreateBillboard(placement);
            }
        }
    }

    private void CreateBillboard(BattlefieldDecorationPlacement placement)
    {
        var isTree = placement.Kind == BattlefieldDecorationKind.Tree;
        var sprite = new Sprite3D
        {
            Name = $"{(isTree ? "Tree" : "Bush")}_{placement.Ordinal:000}",
            Texture = isTree ? _treeTexture : _bushTexture,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = isTree ? 0.030f : 0.022f,
            Modulate = DecorationColor(placement, isTree),
            FlipH = placement.Flipped,
            Position = _projector!.Project(
                placement.XUnits,
                placement.ZUnits,
                isTree ? BattlefieldPresentationProjector.TreeVerticalOffset : BattlefieldPresentationProjector.BushVerticalOffset,
                BattlefieldProjectionPurpose.Decoration),
            Scale = Vector3.One * (placement.ScalePermille / 1_000.0f),
        };
        _foliageRoot!.AddChild(sprite);
    }

    private void CreateRock(BattlefieldDecorationPlacement placement)
    {
        var mesh = new BoxMesh
        {
            Size = new Vector3(
                0.7f * placement.ScalePermille / 1_000.0f,
                0.55f * placement.ScalePermille / 1_000.0f,
                0.9f * placement.ScalePermille / 1_000.0f),
        };
        var rock = new MeshInstance3D
        {
            Name = $"Rock_{placement.Ordinal:000}",
            Mesh = mesh,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(0.24f, 0.25f, 0.23f, 1.0f),
                Roughness = 1.0f,
            },
            Position = _projector!.Project(
                placement.XUnits,
                placement.ZUnits,
                BattlefieldPresentationProjector.PropVerticalOffset,
                BattlefieldProjectionPurpose.Decoration),
            RotationDegrees = new Vector3(0.0f, placement.TintVariant * 31.0f, 0.0f),
        };
        _propsRoot!.AddChild(rock);
        PropCount++;
    }

    private static Color DecorationColor(BattlefieldDecorationPlacement placement, bool tree)
    {
        var baseColor = tree
            ? new Color(0.20f, 0.48f, 0.25f, 0.94f)
            : new Color(0.30f, 0.56f, 0.22f, 0.90f);
        var accent = placement.TintVariant switch
        {
            1 => new Color(1.0f, 0.88f, 0.72f, 1.0f),
            2 => new Color(0.76f, 0.90f, 1.0f, 1.0f),
            _ => Colors.White,
        };
        return new Color(
            baseColor.R * accent.R,
            baseColor.G * accent.G,
            baseColor.B * accent.B,
            baseColor.A);
    }
}
