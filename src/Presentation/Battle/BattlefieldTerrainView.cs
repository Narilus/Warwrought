using System;
using System.Collections.Generic;
using Godot;
using Warwrought.Battle.Model;

namespace Warwrought.Presentation.Battle;

/// <summary>
/// Maintained BattleLab terrain/decor root. The scene provides the required roots; this production
/// node consumes the selected M2.1 profile, builds the visible ArrayMesh, and scatters static
/// authored billboards/primitive props without collision or per-node update scripts.
/// </summary>
public partial class BattlefieldTerrainView : Node3D
{
    public const string GrassClump01TexturePath = "res://assets/environment/m2/foliage/grass_clump_01.png";
    public const string GrassClump02TexturePath = "res://assets/environment/m2/foliage/grass_clump_02.png";
    public const string ShrubLeafy01TexturePath = "res://assets/environment/m2/foliage/shrub_leafy_01.png";
    public const string ShrubScrub01TexturePath = "res://assets/environment/m2/foliage/shrub_scrub_01.png";
    public const string TreeSmall01TexturePath = "res://assets/environment/m2/foliage/tree_small_01.png";
    public const string TreeSmall02TexturePath = "res://assets/environment/m2/foliage/tree_small_02.png";
    public const string DeadBrush01TexturePath = "res://assets/environment/m2/foliage/dead_brush_01.png";

    private static readonly FoliageSpriteProfile[] TreeProfiles =
    {
        new("tree_small_01", TreeSmall01TexturePath, 0.0058f, 222.0f),
        new("tree_small_02", TreeSmall02TexturePath, 0.0055f, 232.0f),
    };

    private static readonly FoliageSpriteProfile[] BushProfiles =
    {
        new("grass_clump_01", GrassClump01TexturePath, 0.0044f, 194.0f),
        new("grass_clump_02", GrassClump02TexturePath, 0.0037f, 224.0f),
        new("shrub_leafy_01", ShrubLeafy01TexturePath, 0.0045f, 211.0f),
        new("shrub_scrub_01", ShrubScrub01TexturePath, 0.0042f, 233.0f),
        new("dead_brush_01", DeadBrush01TexturePath, 0.0044f, 233.0f),
    };

    private static readonly string[] AuthoredTexturePaths =
    {
        GrassClump01TexturePath,
        GrassClump02TexturePath,
        ShrubLeafy01TexturePath,
        ShrubScrub01TexturePath,
        TreeSmall01TexturePath,
        TreeSmall02TexturePath,
        DeadBrush01TexturePath,
    };

    private BattlefieldPresentationProjector? _projector;
    private BattlefieldTerrainPresenter? _presenter;
    private Node3D? _foliageRoot;
    private Node3D? _propsRoot;
    private MeshInstance3D? _ground;
    private MeshInstance3D? _contactLine;
    private readonly Dictionary<string, Texture2D> _foliageTextures = new(StringComparer.Ordinal);

    public bool IsConfigured => _presenter is not null;

    public BattlefieldDefinition Definition => _presenter?.Definition ?? throw new InvalidOperationException("BattlefieldTerrainView has not been configured.");

    public BattlefieldTerrainMeshData MeshData => _presenter?.MeshData ?? throw new InvalidOperationException("BattlefieldTerrainView has not been configured.");

    public int MeshTriangleCount => _presenter?.TriangleCount ?? 0;

    public int MeshVertexCount => _presenter?.VertexCount ?? 0;

    public bool MeshSamplerAgreement => _presenter?.MeshData.HasSamplerAgreement() ?? false;

    public int FoliagePlacementCount { get; private set; }

    public int PropCount { get; private set; }

    public string FoliageDigest { get; private set; } = string.Empty;

    public static IReadOnlyList<string> AuthoredFoliageTexturePaths => AuthoredTexturePaths;

    /// <summary>
    /// Selects an authored source from the immutable placement identity only. The ordinal, kind,
    /// tint variant, and flip flag are already part of the deterministic scatter output; no
    /// engine/global/system random state is consulted here.
    /// </summary>
    public static string SelectFoliageTexturePath(BattlefieldDecorationPlacement placement)
    {
        return SelectFoliageProfile(placement).TexturePath;
    }

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
        _foliageTextures.Clear();
        for (var index = 0; index < AuthoredTexturePaths.Length; index++)
        {
            var texturePath = AuthoredTexturePaths[index];
            _foliageTextures.Add(
                texturePath,
                GD.Load<Texture2D>(texturePath)
                    ?? throw new InvalidOperationException($"BattleLab required authored foliage resource is missing: {texturePath}"));
        }
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
        var profile = SelectFoliageProfile(placement);
        var placementScale = placement.ScalePermille / 1_000.0f;
        var sprite = new Sprite3D
        {
            Name = $"{profile.Id}_{placement.Ordinal:000}",
            Texture = _foliageTextures[profile.TexturePath],
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = profile.PixelSize,
            // The staging sources are transparent RGBA. Keep standard alpha blending so their
            // soft authored edges remain intact, and keep them unshaded so Forward+ lighting does
            // not turn the photographic silhouettes into dark cut-outs.
            Transparent = true,
            AlphaCut = SpriteBase3D.AlphaCutMode.Disabled,
            Shaded = false,
            DoubleSided = true,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Linear,
            Modulate = DecorationColor(placement),
            FlipH = placement.Flipped,
            // The source objects are lower-centred inside their 512x512 canvases. A per-source
            // pixel pivot moves the authored base to the sampled terrain without changing pixels.
            Offset = new Vector2(0.0f, profile.PivotOffsetPixels),
            Position = _projector!.Project(
                placement.XUnits,
                placement.ZUnits,
                0.0f,
                BattlefieldProjectionPurpose.Decoration),
            Scale = Vector3.One * placementScale,
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

    private static FoliageSpriteProfile SelectFoliageProfile(BattlefieldDecorationPlacement placement)
    {
        var variant = placement.Ordinal + (placement.TintVariant * 3) + (placement.Flipped ? 1 : 0);
        return placement.Kind switch
        {
            BattlefieldDecorationKind.Tree => TreeProfiles[variant % TreeProfiles.Length],
            BattlefieldDecorationKind.Bush => BushProfiles[variant % BushProfiles.Length],
            _ => throw new ArgumentOutOfRangeException(nameof(placement), placement.Kind, "Rocks use the existing primitive prop path and do not select a foliage texture."),
        };
    }

    private static Color DecorationColor(BattlefieldDecorationPlacement placement)
    {
        var accent = placement.TintVariant switch
        {
            1 => new Color(1.0f, 0.96f, 0.90f, 1.0f),
            2 => new Color(0.90f, 0.96f, 1.0f, 1.0f),
            _ => Colors.White,
        };
        return accent;
    }

    private readonly record struct FoliageSpriteProfile(
        string Id,
        string TexturePath,
        float PixelSize,
        float PivotOffsetPixels);
}
