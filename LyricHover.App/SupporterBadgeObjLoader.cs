using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace LyricHover.App
{
    internal static class SupporterBadgeObjLoader
    {
        private const string ModelRelativePath = @"Assets\Models\supporter-badge.obj.gz";
        private const double ModelScale = 100.0;

        public static Model3DGroup Load(Func<string, Material> resolveMaterial)
        {
            if (resolveMaterial == null)
            {
                throw new ArgumentNullException(nameof(resolveMaterial));
            }

            var modelPath = Path.Combine(AppContext.BaseDirectory, ModelRelativePath);
            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException(
                    "找不到LyricHover Pro 支持者徽章模型资源。",
                    modelPath);
            }

            using (var file = File.OpenRead(modelPath))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            using (var reader = new StreamReader(gzip))
            {
                return Parse(reader, resolveMaterial);
            }
        }

        private static Model3DGroup Parse(
            TextReader reader,
            Func<string, Material> resolveMaterial)
        {
            var sourcePositions = new List<Point3D>(60000);
            var sourceNormals = new List<Vector3D>(60000);
            var sourceTextureCoordinates = new List<Point>(60000);
            var builders = new List<ObjMeshBuilder>();
            var builderByKey = new Dictionary<string, ObjMeshBuilder>(StringComparer.Ordinal);
            var objectName = "Badge_Model";
            var materialName = "Gold_PBR";

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length < 2 || line[0] == '#')
                {
                    continue;
                }

                if (line.StartsWith("v ", StringComparison.Ordinal))
                {
                    sourcePositions.Add(ParsePosition(line));
                }
                else if (line.StartsWith("vn ", StringComparison.Ordinal))
                {
                    sourceNormals.Add(ParseNormal(line));
                }
                else if (line.StartsWith("vt ", StringComparison.Ordinal))
                {
                    sourceTextureCoordinates.Add(ParseTextureCoordinate(line));
                }
                else if (line.StartsWith("o ", StringComparison.Ordinal))
                {
                    objectName = line.Substring(2).Trim();
                }
                else if (line.StartsWith("usemtl ", StringComparison.Ordinal))
                {
                    materialName = line.Substring(7).Trim();
                }
                else if (line.StartsWith("f ", StringComparison.Ordinal))
                {
                    var key = objectName + "\u001f" + materialName;
                    if (!builderByKey.TryGetValue(key, out var builder))
                    {
                        builder = new ObjMeshBuilder(objectName, materialName);
                        builderByKey.Add(key, builder);
                        builders.Add(builder);
                    }

                    builder.AddFace(
                        line.Substring(2),
                        sourcePositions,
                        sourceNormals,
                        sourceTextureCoordinates);
                }
            }

            var model = new Model3DGroup();
            foreach (var builder in builders)
            {
                var mesh = builder.Build();
                if (mesh.TriangleIndices.Count == 0)
                {
                    continue;
                }

                var material = resolveMaterial(builder.MaterialName);
                var geometry = new GeometryModel3D(mesh, material)
                {
                    BackMaterial = material
                };
                geometry.Freeze();
                model.Children.Add(geometry);
            }

            return model;
        }

        private static Point3D ParsePosition(string line)
        {
            var values = SplitNumbers(line, 2, 3);
            return new Point3D(
                values[0] * ModelScale,
                -values[2] * ModelScale,
                values[1] * ModelScale);
        }

        private static Vector3D ParseNormal(string line)
        {
            var values = SplitNumbers(line, 3, 3);
            var normal = new Vector3D(values[0], -values[2], values[1]);
            normal.Normalize();
            return normal;
        }

        private static Point ParseTextureCoordinate(string line)
        {
            var values = SplitNumbers(line, 3, 2);
            return new Point(values[0], 1.0 - values[1]);
        }

        private static double[] SplitNumbers(string line, int start, int count)
        {
            var parts = line.Substring(start).Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            var result = new double[count];
            for (var index = 0; index < count; index++)
            {
                result[index] = double.Parse(
                    parts[index],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
            }
            return result;
        }

        private sealed class ObjMeshBuilder
        {
            private readonly List<Point3D> positions = new List<Point3D>();
            private readonly List<Vector3D> normals = new List<Vector3D>();
            private readonly List<Point> textureCoordinates = new List<Point>();
            private readonly List<int> triangleIndices = new List<int>();
            private readonly Dictionary<VertexKey, int> vertexIndices =
                new Dictionary<VertexKey, int>();

            public ObjMeshBuilder(string objectName, string materialName)
            {
                ObjectName = objectName;
                MaterialName = materialName;
            }

            public string ObjectName { get; }

            public string MaterialName { get; }

            public void AddFace(
                string face,
                IReadOnlyList<Point3D> sourcePositions,
                IReadOnlyList<Vector3D> sourceNormals,
                IReadOnlyList<Point> sourceTextureCoordinates)
            {
                var tokens = face.Split(
                    new[] { ' ', '\t' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 3)
                {
                    return;
                }

                var faceIndices = new int[tokens.Length];
                for (var index = 0; index < tokens.Length; index++)
                {
                    var key = VertexKey.Parse(
                        tokens[index],
                        sourcePositions.Count,
                        sourceTextureCoordinates.Count,
                        sourceNormals.Count);
                    if (!vertexIndices.TryGetValue(key, out var vertexIndex))
                    {
                        vertexIndex = positions.Count;
                        vertexIndices.Add(key, vertexIndex);
                        positions.Add(sourcePositions[key.PositionIndex]);
                        normals.Add(key.NormalIndex >= 0
                            ? sourceNormals[key.NormalIndex]
                            : new Vector3D(0, 0, 1));
                        textureCoordinates.Add(key.TextureCoordinateIndex >= 0
                            ? sourceTextureCoordinates[key.TextureCoordinateIndex]
                            : new Point(0.5, 0.5));
                    }
                    faceIndices[index] = vertexIndex;
                }

                for (var index = 1; index < faceIndices.Length - 1; index++)
                {
                    triangleIndices.Add(faceIndices[0]);
                    triangleIndices.Add(faceIndices[index]);
                    triangleIndices.Add(faceIndices[index + 1]);
                }
            }

            public MeshGeometry3D Build()
            {
                var mesh = new MeshGeometry3D
                {
                    Positions = new Point3DCollection(positions),
                    Normals = new Vector3DCollection(normals),
                    TextureCoordinates = new PointCollection(textureCoordinates),
                    TriangleIndices = new Int32Collection(triangleIndices)
                };
                mesh.Freeze();
                return mesh;
            }
        }

        private struct VertexKey : IEquatable<VertexKey>
        {
            public VertexKey(
                int positionIndex,
                int textureCoordinateIndex,
                int normalIndex)
            {
                PositionIndex = positionIndex;
                TextureCoordinateIndex = textureCoordinateIndex;
                NormalIndex = normalIndex;
            }

            public int PositionIndex { get; }

            public int TextureCoordinateIndex { get; }

            public int NormalIndex { get; }

            public static VertexKey Parse(
                string token,
                int positionCount,
                int textureCoordinateCount,
                int normalCount)
            {
                var parts = token.Split('/');
                return new VertexKey(
                    ParseIndex(parts[0], positionCount),
                    parts.Length > 1 && parts[1].Length > 0
                        ? ParseIndex(parts[1], textureCoordinateCount)
                        : -1,
                    parts.Length > 2 && parts[2].Length > 0
                        ? ParseIndex(parts[2], normalCount)
                        : -1);
            }

            public bool Equals(VertexKey other)
            {
                return PositionIndex == other.PositionIndex &&
                       TextureCoordinateIndex == other.TextureCoordinateIndex &&
                       NormalIndex == other.NormalIndex;
            }

            public override bool Equals(object value)
            {
                return value is VertexKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = PositionIndex;
                    hash = (hash * 397) ^ TextureCoordinateIndex;
                    return (hash * 397) ^ NormalIndex;
                }
            }

            private static int ParseIndex(string value, int count)
            {
                var index = int.Parse(value, CultureInfo.InvariantCulture);
                return index > 0 ? index - 1 : count + index;
            }
        }
    }
}
