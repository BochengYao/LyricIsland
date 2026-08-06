using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace LyricHover.App
{
    /// <summary>
    /// Small, deliberately scoped glTF 2.0 reader for the frozen supporter badge asset.
    /// It reads the final GLB directly into WPF meshes; it never writes or converts the asset.
    /// </summary>
    internal sealed class SupporterBadgeGlbLoader
    {
        private const uint GlbMagic = 0x46546C67; // glTF
        private const uint JsonChunk = 0x4E4F534A; // JSON
        private const uint BinChunk = 0x004E4942; // BIN\0

        private readonly JsonDocument document;
        private readonly JsonElement root;
        private readonly byte[] binary;
        private readonly string path;
        private readonly Dictionary<int, Matrix4> worldMatrices = new Dictionary<int, Matrix4>();
        private readonly Dictionary<int, Material> materialCache = new Dictionary<int, Material>();

        private SupporterBadgeGlbLoader(string path, JsonDocument document, byte[] binary)
        {
            this.path = path;
            this.document = document;
            root = document.RootElement;
            this.binary = binary;
        }

        public static SupporterBadgeGlbLoadResult Load(string path, Func<SupporterBadgePlaqueInfo, Material> createPlaqueMaterial)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException("The final supporter badge GLB was not found.", path);
            }

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 20 || ReadUInt32(bytes, 0) != GlbMagic || ReadUInt32(bytes, 4) != 2 || ReadUInt32(bytes, 8) != bytes.Length)
            {
                throw new InvalidDataException("The supporter badge asset is not a valid GLB 2.0 container.");
            }

            var jsonLength = checked((int)ReadUInt32(bytes, 12));
            if (ReadUInt32(bytes, 16) != JsonChunk || 20 + jsonLength + 8 > bytes.Length)
            {
                throw new InvalidDataException("The supporter badge GLB JSON chunk is invalid.");
            }

            var json = System.Text.Encoding.UTF8.GetString(bytes, 20, jsonLength).TrimEnd(' ', '\0');
            var binOffset = 20 + jsonLength;
            var binLength = checked((int)ReadUInt32(bytes, binOffset));
            if (ReadUInt32(bytes, binOffset + 4) != BinChunk || binOffset + 8 + binLength != bytes.Length)
            {
                throw new InvalidDataException("The supporter badge GLB BIN chunk is invalid.");
            }

            var binary = new byte[binLength];
            Buffer.BlockCopy(bytes, binOffset + 8, binary, 0, binLength);
            using (var parsed = JsonDocument.Parse(json))
            {
                // Clone the document because the using scope must not own the result.
                var stable = JsonDocument.Parse(parsed.RootElement.GetRawText());
                return new SupporterBadgeGlbLoader(path, stable, binary).Create(createPlaqueMaterial);
            }
        }

        private SupporterBadgeGlbLoadResult Create(Func<SupporterBadgePlaqueInfo, Material> createPlaqueMaterial)
        {
            var sceneIndex = root.TryGetProperty("scene", out var defaultScene) ? defaultScene.GetInt32() : 0;
            var scenes = root.GetProperty("scenes");
            if (sceneIndex < 0 || sceneIndex >= scenes.GetArrayLength())
            {
                throw new InvalidDataException("The supporter badge GLB has no valid default scene.");
            }

            var scene = scenes[sceneIndex];
            foreach (var sceneNode in scene.GetProperty("nodes").EnumerateArray())
            {
                CacheWorldMatrix(sceneNode.GetInt32(), Matrix4.Identity);
            }

            var primitives = new List<PrimitiveData>();
            var nodes = root.GetProperty("nodes");
            for (var nodeIndex = 0; nodeIndex < nodes.GetArrayLength(); nodeIndex++)
            {
                var node = nodes[nodeIndex];
                if (!worldMatrices.TryGetValue(nodeIndex, out var nodeWorld) ||
                    !node.TryGetProperty("mesh", out var meshIndexElement))
                {
                    continue;
                }

                var meshIndex = meshIndexElement.GetInt32();
                var mesh = root.GetProperty("meshes")[meshIndex];
                var nodeName = GetName(node, "node-" + nodeIndex.ToString(CultureInfo.InvariantCulture));
                var meshName = GetName(mesh, "mesh-" + meshIndex.ToString(CultureInfo.InvariantCulture));
                var primitiveIndex = 0;
                foreach (var primitive in mesh.GetProperty("primitives").EnumerateArray())
                {
                    primitives.Add(ReadPrimitive(
                        nodeIndex,
                        nodeName,
                        meshIndex,
                        meshName,
                        primitiveIndex++,
                        primitive,
                        nodeWorld));
                }
            }

            var plaque = LocatePlaque(primitives);
            var plaqueMaterial = createPlaqueMaterial == null || plaque == null
                ? null
                : createPlaqueMaterial(plaque);

            var model = new Model3DGroup();
            foreach (var primitive in primitives)
            {
                AddPrimitiveModels(model, primitive, plaque, plaqueMaterial);
            }

            var bounds = Bounds3.Empty;
            var reachableMeshes = new HashSet<int>();
            foreach (var primitive in primitives)
            {
                bounds.Include(primitive.Bounds);
                reachableMeshes.Add(primitive.MeshIndex);
            }

            var diagnostics = new SupporterBadgeRuntimeDiagnostics(
                "glb",
                Path.GetFullPath(path),
                Sha256(path),
                reachableMeshes.Count,
                root.TryGetProperty("materials", out var materials) ? materials.GetArrayLength() : 0,
                false,
                bounds);
            document.Dispose();
            return new SupporterBadgeGlbLoadResult(model, diagnostics, plaque);
        }

        private void CacheWorldMatrix(int nodeIndex, Matrix4 parent)
        {
            if (worldMatrices.ContainsKey(nodeIndex))
            {
                return;
            }

            var node = root.GetProperty("nodes")[nodeIndex];
            var world = parent * ReadNodeMatrix(node);
            worldMatrices[nodeIndex] = world;
            if (node.TryGetProperty("children", out var children))
            {
                foreach (var child in children.EnumerateArray())
                {
                    CacheWorldMatrix(child.GetInt32(), world);
                }
            }
        }

        private PrimitiveData ReadPrimitive(int nodeIndex, string nodeName, int meshIndex, string meshName, int primitiveIndex, JsonElement primitive, Matrix4 world)
        {
            if (primitive.TryGetProperty("mode", out var mode) && mode.GetInt32() != 4)
            {
                throw new NotSupportedException("The frozen supporter badge uses only triangle primitives.");
            }

            var attributes = primitive.GetProperty("attributes");
            var positions = ReadVector3(attributes.GetProperty("POSITION").GetInt32());
            var normals = attributes.TryGetProperty("NORMAL", out var normalAccessor)
                ? ReadVector3(normalAccessor.GetInt32())
                : null;
            var texcoords = attributes.TryGetProperty("TEXCOORD_0", out var uvAccessor)
                ? ReadVector2(uvAccessor.GetInt32())
                : null;
            var indices = primitive.TryGetProperty("indices", out var indexAccessor)
                ? ReadIndices(indexAccessor.GetInt32())
                : BuildSequentialIndices(positions.Length);
            if (positions.Length == 0 || indices.Length == 0 || indices.Length % 3 != 0)
            {
                throw new InvalidDataException("A supporter badge primitive has invalid triangle data.");
            }

            var transformedPositions = new Point3D[positions.Length];
            var transformedNormals = new Vector3D[positions.Length];
            var bounds = Bounds3.Empty;
            for (var i = 0; i < positions.Length; i++)
            {
                transformedPositions[i] = world.TransformPoint(positions[i]);
                bounds.Include(transformedPositions[i]);
                var normal = normals != null && i < normals.Length
                    ? normals[i]
                    : new Vector3D(0, 0, 1);
                transformedNormals[i] = world.TransformVector(normal).Normalized();
            }

            var materialIndex = primitive.TryGetProperty("material", out var material) ? material.GetInt32() : -1;
            return new PrimitiveData
            {
                NodeIndex = nodeIndex,
                NodeName = nodeName,
                MeshIndex = meshIndex,
                MeshName = meshName,
                PrimitiveIndex = primitiveIndex,
                MaterialIndex = materialIndex,
                MaterialName = MaterialName(materialIndex),
                Positions = transformedPositions,
                Normals = transformedNormals,
                Texcoords = texcoords,
                Indices = indices,
                Bounds = bounds
            };
        }

        private void AddPrimitiveModels(Model3DGroup model, PrimitiveData primitive, SupporterBadgePlaqueInfo plaque, Material plaqueMaterial)
        {
            var usePlaqueReplacement = plaque != null &&
                primitive.NodeIndex == plaque.NodeIndex &&
                primitive.PrimitiveIndex == plaque.PrimitiveIndex &&
                plaqueMaterial != null;
            if (!usePlaqueReplacement)
            {
                model.Children.Add(CreateGeometryModel(primitive, primitive.Indices, MaterialFor(primitive), false));
                return;
            }

            var front = new List<int>();
            var remaining = new List<int>();
            for (var i = 0; i < primitive.Indices.Length; i += 3)
            {
                var normal = (primitive.Normals[primitive.Indices[i]] + primitive.Normals[primitive.Indices[i + 1]] + primitive.Normals[primitive.Indices[i + 2]]).Normalized();
                var target = normal.Z < -0.70 ? front : remaining;
                target.Add(primitive.Indices[i]);
                target.Add(primitive.Indices[i + 1]);
                target.Add(primitive.Indices[i + 2]);
            }

            // The final GLB exposes the plaque face toward -Z. If an exporter has made the
            // plaque a single sheet, applying the replacement to all of its triangles is still
            // correct and does not introduce a second runtime rectangle.
            if (front.Count == 0)
            {
                front.AddRange(primitive.Indices);
            }
            else if (remaining.Count > 0)
            {
                model.Children.Add(CreateGeometryModel(primitive, remaining, MaterialFor(primitive), false));
            }
            model.Children.Add(CreateGeometryModel(primitive, front, plaqueMaterial, true));
        }

        private GeometryModel3D CreateGeometryModel(PrimitiveData primitive, IList<int> indices, Material material, bool usePlaqueUvs)
        {
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection(primitive.Positions),
                Normals = new Vector3DCollection(primitive.Normals),
                TextureCoordinates = usePlaqueUvs
                    ? CreatePlaqueUvs(primitive.Positions, primitive.Bounds)
                    : primitive.Texcoords == null ? CreateDefaultUvs(primitive.Positions.Length) : new PointCollection(primitive.Texcoords),
                TriangleIndices = new Int32Collection(indices)
            };
            mesh.Freeze();
            var result = new GeometryModel3D(mesh, material) { BackMaterial = material };
            result.Freeze();
            return result;
        }

        private SupporterBadgePlaqueInfo LocatePlaque(IEnumerable<PrimitiveData> primitives)
        {
            foreach (var primitive in primitives)
            {
                if (string.Equals(primitive.NodeName, "Badge_Back_NamePlate", StringComparison.Ordinal) &&
                    string.Equals(primitive.MaterialName, "Dark_Inset_PBR", StringComparison.Ordinal))
                {
                    var axes = ExtractPlaqueAxes(primitive);
                    return new SupporterBadgePlaqueInfo(
                        primitive.NodeIndex,
                        primitive.NodeName,
                        primitive.MeshIndex,
                        primitive.MeshName,
                        primitive.PrimitiveIndex,
                        primitive.MaterialName,
                        primitive.Bounds,
                        axes.Width,
                        axes.Height,
                        axes.Normal,
                        axes.Horizontal,
                        axes.Vertical);
                }
            }
            throw new InvalidDataException("The final GLB does not contain its expected deep-navy back name plate.");
        }

        private PlaqueAxes ExtractPlaqueAxes(PrimitiveData primitive)
        {
            var normal = new Vector3D();
            foreach (var value in primitive.Normals)
            {
                if (value.Z < -0.70)
                {
                    normal += value;
                }
            }
            normal = normal.Length > 0 ? normal.Normalized() : new Vector3D(0, 0, -1);
            // The badge export contract is world X right and world Y up. Those are also the
            // plate axes after its final scene transforms have been applied.
            return new PlaqueAxes
            {
                Normal = normal,
                Horizontal = new Vector3D(1, 0, 0),
                Vertical = new Vector3D(0, 1, 0),
                Width = primitive.Bounds.Max.X - primitive.Bounds.Min.X,
                Height = primitive.Bounds.Max.Y - primitive.Bounds.Min.Y
            };
        }

        private Material MaterialFor(PrimitiveData primitive)
        {
            if (string.Equals(primitive.NodeName, "Badge_Front_Inner_Pinstripe", StringComparison.Ordinal))
            {
                // The GLB geometry/normals are already smooth and valid.  WPF Viewport3D
                // has no MSAA control, so its extremely thin, high-contrast gold pinstripe
                // aliases into a dotted bright ring.  This is a WPF-only, lower-specular
                // approximation; it does not alter the GLB material, mesh or dimensions.
                return SupporterBadge3DFactory.CreateWpfMaterial(
                    "Gold_Inner_Pinstripe_Wpf",
                    Color.FromRgb(169, 132, 75),
                    0.82,
                    0.65);
            }

            var materialIndex = primitive.MaterialIndex;
            if (materialCache.TryGetValue(materialIndex, out var cached))
            {
                return cached;
            }
            var name = MaterialName(materialIndex);
            var color = MaterialColor(materialIndex);
            var material = SupporterBadge3DFactory.CreateWpfMaterial(name, color, MaterialRoughness(materialIndex), MaterialMetallic(materialIndex));
            materialCache[materialIndex] = material;
            return material;
        }

        private Color MaterialColor(int materialIndex)
        {
            if (materialIndex >= 0 && root.TryGetProperty("materials", out var materials) && materialIndex < materials.GetArrayLength())
            {
                var material = materials[materialIndex];
                if (material.TryGetProperty("pbrMetallicRoughness", out var pbr) && pbr.TryGetProperty("baseColorFactor", out var factor))
                {
                    var values = ReadNumbers(factor, 4);
                    return Color.FromRgb(ToSrgb(values[0]), ToSrgb(values[1]), ToSrgb(values[2]));
                }
            }
            return Color.FromRgb(186, 137, 47);
        }

        private double MaterialRoughness(int materialIndex)
        {
            return MaterialPbrNumber(materialIndex, "roughnessFactor", 0.5);
        }

        private double MaterialMetallic(int materialIndex)
        {
            return MaterialPbrNumber(materialIndex, "metallicFactor", 1.0);
        }

        private double MaterialPbrNumber(int materialIndex, string property, double fallback)
        {
            if (materialIndex >= 0 && root.TryGetProperty("materials", out var materials) && materialIndex < materials.GetArrayLength())
            {
                var material = materials[materialIndex];
                if (material.TryGetProperty("pbrMetallicRoughness", out var pbr) && pbr.TryGetProperty(property, out var value))
                {
                    return value.GetDouble();
                }
            }
            return fallback;
        }

        private string MaterialName(int materialIndex)
        {
            if (materialIndex >= 0 && root.TryGetProperty("materials", out var materials) && materialIndex < materials.GetArrayLength())
            {
                return GetName(materials[materialIndex], "material-" + materialIndex.ToString(CultureInfo.InvariantCulture));
            }
            return "default";
        }

        private Vector3D[] ReadVector3(int accessorIndex)
        {
            var accessor = Accessor(accessorIndex);
            Require(accessor, "VEC3", 5126);
            var result = new Vector3D[accessor.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var offset = accessor.Offset + i * accessor.Stride;
                result[i] = new Vector3D(ReadFloat(binary, offset), ReadFloat(binary, offset + 4), ReadFloat(binary, offset + 8));
            }
            return result;
        }

        private Point[] ReadVector2(int accessorIndex)
        {
            var accessor = Accessor(accessorIndex);
            Require(accessor, "VEC2", 5126);
            var result = new Point[accessor.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var offset = accessor.Offset + i * accessor.Stride;
                result[i] = new Point(ReadFloat(binary, offset), ReadFloat(binary, offset + 4));
            }
            return result;
        }

        private int[] ReadIndices(int accessorIndex)
        {
            var accessor = Accessor(accessorIndex);
            if (accessor.Type != "SCALAR" || (accessor.ComponentType != 5121 && accessor.ComponentType != 5123 && accessor.ComponentType != 5125))
            {
                throw new InvalidDataException("A supporter badge index accessor is invalid.");
            }
            var size = accessor.ComponentType == 5121 ? 1 : accessor.ComponentType == 5123 ? 2 : 4;
            var result = new int[accessor.Count];
            for (var i = 0; i < result.Length; i++)
            {
                var offset = accessor.Offset + i * accessor.Stride;
                result[i] = size == 1 ? binary[offset] : size == 2 ? ReadUInt16(binary, offset) : checked((int)ReadUInt32(binary, offset));
            }
            return result;
        }

        private AccessorInfo Accessor(int accessorIndex)
        {
            var accessor = root.GetProperty("accessors")[accessorIndex];
            var view = root.GetProperty("bufferViews")[accessor.GetProperty("bufferView").GetInt32()];
            var componentType = accessor.GetProperty("componentType").GetInt32();
            var type = accessor.GetProperty("type").GetString();
            var components = type == "SCALAR" ? 1 : type == "VEC2" ? 2 : type == "VEC3" ? 3 : 4;
            var componentSize = componentType == 5121 ? 1 : componentType == 5123 ? 2 : 4;
            var elementSize = components * componentSize;
            var stride = view.TryGetProperty("byteStride", out var byteStride) ? byteStride.GetInt32() : elementSize;
            var offset = (view.TryGetProperty("byteOffset", out var viewOffset) ? viewOffset.GetInt32() : 0) + (accessor.TryGetProperty("byteOffset", out var accessorOffset) ? accessorOffset.GetInt32() : 0);
            return new AccessorInfo { ComponentType = componentType, Type = type, Count = accessor.GetProperty("count").GetInt32(), Offset = offset, Stride = stride };
        }

        private static PointCollection CreateDefaultUvs(int count)
        {
            var result = new PointCollection(count);
            for (var i = 0; i < count; i++) result.Add(new Point(0, 0));
            return result;
        }

        private static PointCollection CreatePlaqueUvs(IReadOnlyList<Point3D> positions, Bounds3 bounds)
        {
            var width = bounds.Max.X - bounds.Min.X;
            var height = bounds.Max.Y - bounds.Min.Y;
            var result = new PointCollection(positions.Count);
            foreach (var position in positions)
            {
                // Explicit WPF runtime UV contract for the plaque front: U spans the final
                // world X width and V spans final world Y height.  The image's matching aspect
                // makes this a proportional mapping, with no Fill/ScaleY distortion.
                // The rear face is viewed after the badge's 180-degree yaw; reverse U once
                // so the direct plaque material remains readable from the back view.
                result.Add(new Point(1.0 - (position.X - bounds.Min.X) / width, 1.0 - (position.Y - bounds.Min.Y) / height));
            }
            return result;
        }

        private static int[] BuildSequentialIndices(int count)
        {
            var result = new int[count];
            for (var i = 0; i < count; i++) result[i] = i;
            return result;
        }

        private static void Require(AccessorInfo accessor, string type, int componentType)
        {
            if (accessor.Type != type || accessor.ComponentType != componentType || accessor.Count <= 0)
            {
                throw new InvalidDataException("A supporter badge vertex accessor is invalid.");
            }
        }

        private static string GetName(JsonElement element, string fallback)
        {
            return element.TryGetProperty("name", out var name) ? name.GetString() ?? fallback : fallback;
        }

        private static double[] ReadNumbers(JsonElement value, int count)
        {
            var result = new double[count];
            var index = 0;
            foreach (var item in value.EnumerateArray())
            {
                if (index == count) break;
                result[index++] = item.GetDouble();
            }
            return result;
        }

        private static byte ToSrgb(double linear)
        {
            var srgb = linear <= 0.0031308 ? linear * 12.92 : 1.055 * Math.Pow(linear, 1.0 / 2.4) - 0.055;
            return (byte)Math.Max(0, Math.Min(255, Math.Round(srgb * 255.0)));
        }

        private static uint ReadUInt32(byte[] bytes, int offset) => BitConverter.ToUInt32(bytes, offset);
        private static ushort ReadUInt16(byte[] bytes, int offset) => BitConverter.ToUInt16(bytes, offset);
        private static float ReadFloat(byte[] bytes, int offset) => BitConverter.ToSingle(bytes, offset);
        private static string Sha256(string source)
        {
            using (var algorithm = SHA256.Create())
            using (var stream = File.OpenRead(source))
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private sealed class AccessorInfo { public int ComponentType; public string Type; public int Count; public int Offset; public int Stride; }
        private sealed class PrimitiveData
        {
            public int NodeIndex; public string NodeName; public int MeshIndex; public string MeshName; public int PrimitiveIndex; public int MaterialIndex; public string MaterialName;
            public Point3D[] Positions; public Vector3D[] Normals; public Point[] Texcoords; public int[] Indices; public Bounds3 Bounds;
        }
        private sealed class PlaqueAxes { public Vector3D Normal; public Vector3D Horizontal; public Vector3D Vertical; public double Width; public double Height; }

        private struct Matrix4
        {
            public static readonly Matrix4 Identity = new Matrix4 { M = new[] { 1d, 0, 0, 0, 0, 1d, 0, 0, 0, 0, 1d, 0, 0, 0, 0, 1d } };
            public double[] M;
            public static Matrix4 operator *(Matrix4 left, Matrix4 right)
            {
                var result = new double[16];
                for (var column = 0; column < 4; column++) for (var row = 0; row < 4; row++)
                {
                    result[column * 4 + row] = left.M[row] * right.M[column * 4] + left.M[4 + row] * right.M[column * 4 + 1] + left.M[8 + row] * right.M[column * 4 + 2] + left.M[12 + row] * right.M[column * 4 + 3];
                }
                return new Matrix4 { M = result };
            }
            public Point3D TransformPoint(Vector3D point)
            {
                return new Point3D(M[0] * point.X + M[4] * point.Y + M[8] * point.Z + M[12], M[1] * point.X + M[5] * point.Y + M[9] * point.Z + M[13], M[2] * point.X + M[6] * point.Y + M[10] * point.Z + M[14]);
            }
            public Vector3D TransformVector(Vector3D vector)
            {
                return new Vector3D(M[0] * vector.X + M[4] * vector.Y + M[8] * vector.Z, M[1] * vector.X + M[5] * vector.Y + M[9] * vector.Z, M[2] * vector.X + M[6] * vector.Y + M[10] * vector.Z);
            }
        }

        private static Matrix4 ReadNodeMatrix(JsonElement node)
        {
            if (node.TryGetProperty("matrix", out var matrix)) return new Matrix4 { M = ReadNumbers(matrix, 16) };
            var translation = node.TryGetProperty("translation", out var t) ? ReadNumbers(t, 3) : new[] { 0d, 0d, 0d };
            var rotation = node.TryGetProperty("rotation", out var r) ? ReadNumbers(r, 4) : new[] { 0d, 0d, 0d, 1d };
            var scale = node.TryGetProperty("scale", out var s) ? ReadNumbers(s, 3) : new[] { 1d, 1d, 1d };
            var x = rotation[0]; var y = rotation[1]; var z = rotation[2]; var w = rotation[3];
            return new Matrix4 { M = new[] {
                (1-2*y*y-2*z*z)*scale[0], (2*x*y+2*z*w)*scale[0], (2*x*z-2*y*w)*scale[0], 0d,
                (2*x*y-2*z*w)*scale[1], (1-2*x*x-2*z*z)*scale[1], (2*y*z+2*x*w)*scale[1], 0d,
                (2*x*z+2*y*w)*scale[2], (2*y*z-2*x*w)*scale[2], (1-2*x*x-2*y*y)*scale[2], 0d,
                translation[0], translation[1], translation[2], 1d } };
        }
    }

    internal sealed class SupporterBadgeGlbLoadResult
    {
        public SupporterBadgeGlbLoadResult(Model3DGroup model, SupporterBadgeRuntimeDiagnostics diagnostics, SupporterBadgePlaqueInfo plaque) { Model = model; Diagnostics = diagnostics; Plaque = plaque; }
        public Model3DGroup Model { get; }
        public SupporterBadgeRuntimeDiagnostics Diagnostics { get; }
        public SupporterBadgePlaqueInfo Plaque { get; }
    }

    internal sealed class SupporterBadgeRuntimeDiagnostics
    {
        public SupporterBadgeRuntimeDiagnostics(string format, string path, string sha256, int meshCount, int materialCount, bool loadedFromLegacyObj, Bounds3 bounds)
        { BadgeAssetFormat = format; BadgeAssetPath = path; BadgeAssetSha256 = sha256; BadgeMeshCount = meshCount; BadgeMaterialCount = materialCount; BadgeLoadedFromLegacyObj = loadedFromLegacyObj; WorldBounds = bounds; }
        public string BadgeAssetFormat { get; }
        public string BadgeAssetPath { get; }
        public string BadgeAssetSha256 { get; }
        public int BadgeMeshCount { get; }
        public int BadgeMaterialCount { get; }
        public bool BadgeLoadedFromLegacyObj { get; }
        public Bounds3 WorldBounds { get; }
    }

    internal sealed class SupporterBadgePlaqueInfo
    {
        public SupporterBadgePlaqueInfo(int nodeIndex, string nodeName, int meshIndex, string meshName, int primitiveIndex, string materialName, Bounds3 bounds, double width, double height, Vector3D normal, Vector3D horizontal, Vector3D vertical)
        { NodeIndex = nodeIndex; NodeName = nodeName; MeshIndex = meshIndex; MeshName = meshName; PrimitiveIndex = primitiveIndex; MaterialName = materialName; Bounds = bounds; Width = width; Height = height; Normal = normal; Horizontal = horizontal; Vertical = vertical; }
        public int NodeIndex { get; } public string NodeName { get; } public int MeshIndex { get; } public string MeshName { get; } public int PrimitiveIndex { get; } public string MaterialName { get; }
        public Bounds3 Bounds { get; } public Point3D Center => Bounds.Center; public double Width { get; } public double Height { get; } public double AspectRatio => Width / Height; public Vector3D Normal { get; } public Vector3D Horizontal { get; } public Vector3D Vertical { get; }
    }

    internal struct Bounds3
    {
        public static readonly Bounds3 Empty = new Bounds3 { Min = new Point3D(double.PositiveInfinity, double.PositiveInfinity, double.PositiveInfinity), Max = new Point3D(double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity) };
        public Point3D Min; public Point3D Max;
        public Point3D Center => new Point3D((Min.X + Max.X) / 2, (Min.Y + Max.Y) / 2, (Min.Z + Max.Z) / 2);
        public void Include(Point3D point) { Min = new Point3D(Math.Min(Min.X, point.X), Math.Min(Min.Y, point.Y), Math.Min(Min.Z, point.Z)); Max = new Point3D(Math.Max(Max.X, point.X), Math.Max(Max.Y, point.Y), Math.Max(Max.Z, point.Z)); }
        public void Include(Bounds3 other) { Include(other.Min); Include(other.Max); }
    }

    internal static class Vector3DExtensions
    {
        public static Vector3D Normalized(this Vector3D vector) { return vector.Length < 0.0000001 ? new Vector3D(0, 0, 1) : vector / vector.Length; }
    }
}
