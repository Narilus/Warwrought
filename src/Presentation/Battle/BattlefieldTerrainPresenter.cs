using System;
using Godot;
using Warwrought.Battle.Model;

namespace Warwrought.Presentation.Battle;

/// <summary>
/// Godot-facing procedural terrain presenter. ArrayMesh data is assembled from the tested coarse
/// mesh input, with duplicated triangle vertices and explicit face normals/colors for a faceted
/// surface. The mesh has no collision and is never queried for troop height.
/// </summary>
public sealed class BattlefieldTerrainPresenter
{
    private static readonly Color LowRegionColor = new(0.24f, 0.42f, 0.29f, 1.0f);
    private static readonly Color MiddleRegionColor = new(0.47f, 0.50f, 0.28f, 1.0f);
    private static readonly Color HighRegionColor = new(0.38f, 0.34f, 0.28f, 1.0f);

    public BattlefieldTerrainPresenter(BattlefieldPresentationProjector projector)
    {
        ArgumentNullException.ThrowIfNull(projector);
        Projector = projector;
        Definition = projector.Definition;
        MeshData = BattlefieldTerrainMeshData.Build(Definition);
    }

    public BattlefieldDefinition Definition { get; }

    public BattlefieldTerrainMeshData MeshData { get; }

    public BattlefieldPresentationProjector Projector { get; }

    public int TriangleCount => MeshData.TriangleCount;

    public int VertexCount => MeshData.VertexCount;

    public ArrayMesh BuildMesh()
    {
        var vertices = new Vector3[MeshData.VertexCount];
        var normals = new Vector3[MeshData.VertexCount];
        var colors = new Color[MeshData.VertexCount];
        var indices = new int[MeshData.VertexCount];
        var vertexIndex = 0;

        for (var triangleIndex = 0; triangleIndex < MeshData.Triangles.Count; triangleIndex++)
        {
            var triangle = MeshData.Triangles[triangleIndex];
            var a = Projector.ProjectGridVertex(triangle.A.GridXIndex, triangle.A.GridZIndex, 0.0f, BattlefieldProjectionPurpose.TerrainMesh);
            var b = Projector.ProjectGridVertex(triangle.B.GridXIndex, triangle.B.GridZIndex, 0.0f, BattlefieldProjectionPurpose.TerrainMesh);
            var c = Projector.ProjectGridVertex(triangle.C.GridXIndex, triangle.C.GridZIndex, 0.0f, BattlefieldProjectionPurpose.TerrainMesh);
            var normal = (b - a).Cross(c - a).Normalized();
            if (normal.Y < 0.0f)
            {
                normal = -normal;
            }

            AddTriangleVertex(triangle.A, a, normal, vertices, normals, colors, indices, ref vertexIndex);
            AddTriangleVertex(triangle.B, b, normal, vertices, normals, colors, indices, ref vertexIndex);
            AddTriangleVertex(triangle.C, c, normal, vertices, normals, colors, indices, ref vertexIndex);
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    public static StandardMaterial3D CreateMaterial()
    {
        return new StandardMaterial3D
        {
            VertexColorUseAsAlbedo = true,
            AlbedoColor = new Color(0.72f, 0.78f, 0.66f, 1.0f),
            Roughness = 0.94f,
            Metallic = 0.0f,
        };
    }

    private static void AddTriangleVertex(
        BattlefieldTerrainMeshVertex source,
        Vector3 position,
        Vector3 normal,
        Vector3[] vertices,
        Vector3[] normals,
        Color[] colors,
        int[] indices,
        ref int vertexIndex)
    {
        vertices[vertexIndex] = position;
        normals[vertexIndex] = normal;
        colors[vertexIndex] = ColorForRegion(source.TerrainRegionId);
        indices[vertexIndex] = vertexIndex;
        vertexIndex++;
    }

    private static Color ColorForRegion(Warwrought.Core.StableId regionId)
    {
        return regionId.Value switch
        {
            "terrain.low" => LowRegionColor,
            "terrain.high" => HighRegionColor,
            _ => MiddleRegionColor,
        };
    }
}
