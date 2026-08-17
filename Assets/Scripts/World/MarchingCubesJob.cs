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

    public NativeList<float3> vertices;
    public NativeList<int> triangles;
    public NativeList<Color> vertexColors; // Для вывода цветов

    private struct VoxelPoint
    {
        public float3 pos;
        public float density;
        public float3 biomeWeights; // ИЗМЕНЕНО: теперь передаем веса
    }

    public void Execute()
    {
        for (int z = 0; z < chunkSize.z - 1; z++)
        {
            for (int y = 0; y < chunkSize.y - 1; y++)
            {
                for (int x = 0; x < chunkSize.x - 1; x++)
                {
                    VoxelPoint[] corners = new VoxelPoint[8];
                    int cubeIndex = 0;

                    // Получаем 8 вершин куба
                    for (int i = 0; i < 8; i++)
                    {
                        int ix = x + (i == 1 || i == 2 || i == 5 || i == 6 ? 1 : 0);
                        int iy = y + (i == 4 || i == 5 || i == 6 || i == 7 ? 1 : 0);
                        int iz = z + (i == 2 || i == 3 || i == 6 || i == 7 ? 1 : 0);

                        int index = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);

                        corners[i].pos = new float3(ix, iy, iz);
                        corners[i].density = voxels[index].density;
                        corners[i].biomeWeights = voxels[index].biomeWeights;

                        if (corners[i].density > isoLevel) cubeIndex |= (1 << i);
                    }

                    if (cubeIndex == 0 || cubeIndex == 255) continue;

                    int tableIndex = cubeIndex * 16;

                    for (int i = 0; MarchingCubesTables.triTable[tableIndex + i] != -1; i += 3)
                    {
                        int e1 = MarchingCubesTables.triTable[tableIndex + i];
                        int e2 = MarchingCubesTables.triTable[tableIndex + i + 1];
                        int e3 = MarchingCubesTables.triTable[tableIndex + i + 2];

                        InterpolateVertex(e1, corners, out float3 v1, out float3 c1);
                        InterpolateVertex(e2, corners, out float3 v2, out float3 c2);
                        InterpolateVertex(e3, corners, out float3 v3, out float3 c3);

                        triangles.Add(vertices.Length); vertices.Add(v1); vertexColors.Add(new Color(c1.x, c1.y, c1.z));
                        triangles.Add(vertices.Length); vertices.Add(v2); vertexColors.Add(new Color(c2.x, c2.y, c2.z));
                        triangles.Add(vertices.Length); vertices.Add(v3); vertexColors.Add(new Color(c3.x, c3.y, c3.z));
                    }
                }
            }
        }
    }

    private void InterpolateVertex(int edgeIndex, VoxelPoint[] corners, out float3 vert, out float3 weights)
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
        vert = pA.pos + t * (pB.pos - pA.pos);
        weights = pA.biomeWeights + t * (pB.biomeWeights - pA.biomeWeights);
    }
}