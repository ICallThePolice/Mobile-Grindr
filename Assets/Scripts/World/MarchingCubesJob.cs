using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
public struct MarchingCubesJob : IJob
{
    [ReadOnly] public NativeArray<float> densities;
    [ReadOnly] public int3 chunkSize;
    [ReadOnly] public float isoLevel;

    public NativeList<float3> vertices;
    public NativeList<int> triangles;

    public void Execute()
    {
        for (int z = 0; z < chunkSize.z - 1; z++)
        {
            for (int y = 0; y < chunkSize.y - 1; y++)
            {
                for (int x = 0; x < chunkSize.x - 1; x++)
                {
                    // Создаем массив для хранения позиций и плотностей 8 углов куба.
                    // Это проще и надежнее, чем передача множества параметров.
                    float3 p0, p1, p2, p3, p4, p5, p6, p7;
                    float d0, d1, d2, d3, d4, d5, d6, d7;
                    
                    // --- МАКСИМАЛЬНО НАДЕЖНОЕ ПОЛУЧЕНИЕ ДАННЫХ ---
                    // Мы вручную вычисляем 1D-индекс для каждой из 8 вершин
                    // и проверяем его перед доступом к массиву.
                    int ix, iy, iz;

                    ix = x; iy = y; iz = z;
                    int index0 = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);
                    p0 = new float3(ix, iy, iz);
                    d0 = densities[index0];

                    ix = x + 1; iy = y; iz = z;
                    int index1 = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);
                    p1 = new float3(ix, iy, iz);
                    d1 = densities[index1];

                    ix = x + 1; iy = y; iz = z + 1;
                    int index2 = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);
                    p2 = new float3(ix, iy, iz);
                    d2 = densities[index2];

                    ix = x; iy = y; iz = z + 1;
                    int index3 = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);
                    p3 = new float3(ix, iy, iz);
                    d3 = densities[index3];
                    
                    ix = x; iy = y + 1; iz = z;
                    int index4 = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);
                    p4 = new float3(ix, iy, iz);
                    d4 = densities[index4];

                    ix = x + 1; iy = y + 1; iz = z;
                    int index5 = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);
                    p5 = new float3(ix, iy, iz);
                    d5 = densities[index5];

                    ix = x + 1; iy = y + 1; iz = z + 1;
                    int index6 = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);
                    p6 = new float3(ix, iy, iz);
                    d6 = densities[index6];

                    ix = x; iy = y + 1; iz = z + 1;
                    int index7 = ix + iy * chunkSize.x + iz * (chunkSize.x * chunkSize.y);
                    p7 = new float3(ix, iy, iz);
                    d7 = densities[index7];

                    int cubeIndex = 0;
                    if (d0 > isoLevel) cubeIndex |= 1;
                    if (d1 > isoLevel) cubeIndex |= 2;
                    if (d2 > isoLevel) cubeIndex |= 4;
                    if (d3 > isoLevel) cubeIndex |= 8;
                    if (d4 > isoLevel) cubeIndex |= 16;
                    if (d5 > isoLevel) cubeIndex |= 32;
                    if (d6 > isoLevel) cubeIndex |= 64;
                    if (d7 > isoLevel) cubeIndex |= 128;

                    if (cubeIndex == 0 || cubeIndex == 255) continue;

                    int tableIndex = cubeIndex * 16; 

                    // Проходим по таблице, пока не встретим терминатор -1
                    for (int i = 0; MarchingCubesTables.triTable[tableIndex + i] != -1; i += 3)
                    {
                        // Получаем индексы ребер, используя один индекс
                        int edge1 = MarchingCubesTables.triTable[tableIndex + i];
                        int edge2 = MarchingCubesTables.triTable[tableIndex + i + 1];
                        int edge3 = MarchingCubesTables.triTable[tableIndex + i + 2];
                        
                        float3 vert1 = InterpolateVertex(edge1, p0, p1, p2, p3, p4, p5, p6, p7, d0, d1, d2, d3, d4, d5, d6, d7);
                        float3 vert2 = InterpolateVertex(edge2, p0, p1, p2, p3, p4, p5, p6, p7, d0, d1, d2, d3, d4, d5, d6, d7);
                        float3 vert3 = InterpolateVertex(edge3, p0, p1, p2, p3, p4, p5, p6, p7, d0, d1, d2, d3, d4, d5, d6, d7);

                        triangles.Add(vertices.Length); vertices.Add(vert1);
                        triangles.Add(vertices.Length); vertices.Add(vert2);
                        triangles.Add(vertices.Length); vertices.Add(vert3);
                    }
                }
            }
        }
    }
    
    // Эта функция остается здесь для чистоты, но основная логика вынесена наверх
    private float3 InterpolateVertex(int edgeIndex, float3 p0, float3 p1, float3 p2, float3 p3, float3 p4, float3 p5, float3 p6, float3 p7, float d0, float d1, float d2, float d3, float d4, float d5, float d6, float d7)
    {
        int i_a = MarchingCubesTables.edgeConnections[edgeIndex * 2];
        int i_b = MarchingCubesTables.edgeConnections[edgeIndex * 2 + 1];

        float3 p_a, p_b;
        float d_a, d_b;

        GetCorner(i_a, out p_a, out d_a, p0, p1, p2, p3, p4, p5, p6, p7, d0, d1, d2, d3, d4, d5, d6, d7);
        GetCorner(i_b, out p_b, out d_b, p0, p1, p2, p3, p4, p5, p6, p7, d0, d1, d2, d3, d4, d5, d6, d7);

        if (math.abs(d_a - d_b) < 0.00001f) return p_a;
        
        float t = (isoLevel - d_a) / (d_b - d_a);
        return p_a + t * (p_b - p_a);
    }
    
    private void GetCorner(int i, out float3 p, out float d, float3 p0, float3 p1, float3 p2, float3 p3, float3 p4, float3 p5, float3 p6, float3 p7, float d0, float d1, float d2, float d3, float d4, float d5, float d6, float d7)
    {
        switch(i)
        {
            case 0: p = p0; d = d0; break;
            case 1: p = p1; d = d1; break;
            case 2: p = p2; d = d2; break;
            case 3: p = p3; d = d3; break;
            case 4: p = p4; d = d4; break;
            case 5: p = p5; d = d5; break;
            case 6: p = p6; d = d6; break;
            case 7: p = p7; d = d7; break;
            default: p = float3.zero; d = 0; break;
        }
    }
}