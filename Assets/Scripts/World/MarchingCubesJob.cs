using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
public struct MarchingCubesJob : IJob
{
    [ReadOnly] public NativeArray<VoxelData> voxels;
    [ReadOnly] public int3 chunkSize;
    [ReadOnly] public float isoLevel;

    // Списки для Земли
    public NativeList<float3> vertices;
    public NativeList<int> triangles;
    public NativeList<Color> vertexColors;

    // Списки для Жидкостей
    public NativeList<float3> liquidVertices;
    public NativeList<int> liquidTriangles;
    public NativeList<Color> liquidColors;

    private struct VoxelPoint
    {
        public float3 pos;
        public float density;
        public float3 biomeWeights;
    }

    public void Execute()
    {
        NativeArray<VoxelPoint> corners = new NativeArray<VoxelPoint>(8, Allocator.Temp);
        NativeArray<VoxelPoint> liquidCorners = new NativeArray<VoxelPoint>(8, Allocator.Temp);

        for (int z = 0; z < chunkSize.z - 1; z++)
        {
            for (int y = 0; y < chunkSize.y - 1; y++)
            {
                for (int x = 0; x < chunkSize.x - 1; x++)
                {
                    int cubeIndex = 0;
                    int liquidCubeIndex = 0;
                    bool isExposedToAir = false;

                    for (int i = 0; i < 8; i++)
                    {
                        int ix = x + (i == 1 || i == 2 || i == 5 || i == 6 ? 1 : 0);
                        int iy = y + (i == 4 || i == 5 || i == 6 || i == 7 ? 1 : 0);
                        int iz = z + (i == 2 || i == 3 || i == 6 || i == 7 ? 1 : 0);

                        int index = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);

                        // Собираем вектор весов биома из выровненных флоатов
                        float3 weights = new float3(
                            voxels[index].biomeWeightR,
                            voxels[index].biomeWeightG,
                            voxels[index].biomeWeightB
                        );

                        // Углы земли
                        VoxelPoint cp = corners[i];
                        cp.pos = new float3(ix, iy, iz);
                        cp.density = voxels[index].density;
                        cp.biomeWeights = weights;
                        corners[i] = cp;

                        // Углы жидкости
                        VoxelPoint lp = liquidCorners[i];
                        lp.pos = cp.pos;
                        lp.density = voxels[index].liquidDensity;
                        lp.biomeWeights = weights;
                        liquidCorners[i] = lp;

                        if (cp.density > isoLevel) cubeIndex |= (1 << i);
                        else isExposedToAir = true; // Воздух рядом, значит может быть вода

                        if (lp.density > isoLevel) liquidCubeIndex |= (1 << i);
                    }

                    // --- ГЕНЕРАЦИЯ ЗЕМЛИ ---
                    if (cubeIndex != 0 && cubeIndex != 255)
                    {
                        int tableIndex = cubeIndex * 16;
                        for (int i = 0; MarchingCubesTables.triTable[tableIndex + i] != -1; i += 3)
                        {
                            int e1 = MarchingCubesTables.triTable[tableIndex + i];
                            int e2 = MarchingCubesTables.triTable[tableIndex + i + 1];
                            int e3 = MarchingCubesTables.triTable[tableIndex + i + 2];

                            InterpolateVertex(e1, corners, out float3 v1, out float3 c1);
                            InterpolateVertex(e2, corners, out float3 v2, out float3 c2);
                            InterpolateVertex(e3, corners, out float3 v3, out float3 c3);

                            triangles.Add(vertices.Length); vertices.Add(v1); vertexColors.Add(new Color(c1.x, c1.y, c1.z, 1f));
                            triangles.Add(vertices.Length); vertices.Add(v2); vertexColors.Add(new Color(c2.x, c2.y, c2.z, 1f));
                            triangles.Add(vertices.Length); vertices.Add(v3); vertexColors.Add(new Color(c3.x, c3.y, c3.z, 1f));
                        }
                    }

                    // --- ГЕНЕРАЦИЯ ЖИДКОСТИ ---
                    if (isExposedToAir && liquidCubeIndex != 0 && liquidCubeIndex != 255)
                    {
                        int tableIndex = liquidCubeIndex * 16;
                        for (int i = 0; MarchingCubesTables.triTable[tableIndex + i] != -1; i += 3)
                        {
                            int e1 = MarchingCubesTables.triTable[tableIndex + i];
                            int e2 = MarchingCubesTables.triTable[tableIndex + i + 1];
                            int e3 = MarchingCubesTables.triTable[tableIndex + i + 2];

                            InterpolateVertex(e1, liquidCorners, out float3 v1, out float3 c1);
                            InterpolateVertex(e2, liquidCorners, out float3 v2, out float3 c2);
                            InterpolateVertex(e3, liquidCorners, out float3 v3, out float3 c3);

                            Color col1 = BlendLiquidColor(c1);
                            Color col2 = BlendLiquidColor(c2);
                            Color col3 = BlendLiquidColor(c3);

                            liquidTriangles.Add(liquidVertices.Length); liquidVertices.Add(v1); liquidColors.Add(col1);
                            liquidTriangles.Add(liquidVertices.Length); liquidVertices.Add(v2); liquidColors.Add(col2);
                            liquidTriangles.Add(liquidVertices.Length); liquidVertices.Add(v3); liquidColors.Add(col3);
                        }
                    }
                }
            }
        }

        corners.Dispose();
        liquidCorners.Dispose();
    }

    private Color BlendLiquidColor(float3 weights)
    {
        // Vital (R) -> Матовый желто-рыжий (Альфа = 1.0)
        Color vitalColor = new Color(1.0f, 0.6f, 0.1f, 1.0f);
        // Ereb (G) -> Полупрозрачная черная жижа (Альфа = 0.8)
        Color erebColor = new Color(0.05f, 0.05f, 0.05f, 0.8f);
        // Psy (B) -> Нет жидкости, полная прозрачность
        Color psyColor = new Color(0f, 0f, 0f, 0f);

        return (vitalColor * weights.x) + (erebColor * weights.y) + (psyColor * weights.z);
    }

    private void InterpolateVertex(int edgeIndex, NativeArray<VoxelPoint> corners, out float3 vert, out float3 weights)
    {
        int i_a = MarchingCubesTables.edgeConnections[edgeIndex * 2];
        int i_b = MarchingCubesTables.edgeConnections[edgeIndex * 2 + 1];

        VoxelPoint pA = corners[i_a];
        VoxelPoint pB = corners[i_b];

        if (math.abs(pA.density - pB.density) < 0.00001f)
        {
            vert = pA.pos;
            weights = pA.biomeWeights;
            return;
        }

        float t = (isoLevel - pA.density) / (pB.density - pA.density);
        t = math.clamp(t, 0f, 1f);
        vert = pA.pos + t * (pB.pos - pA.pos);
        weights = pA.biomeWeights + t * (pB.biomeWeights - pA.biomeWeights);
    }
}