using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using System;

sealed class RayTracingInstanceData : IDisposable
{
    struct AnimationJob : IJobParallelFor
    {
        public NativeArray<Matrix4x4> matrices;
        public float time;

        public void Execute(int index)
        {
            Vector3 pos = matrices[index].GetPosition();
            pos.y = 2.0f * math.sin(math.sqrt(pos.x * pos.x + pos.z * pos.z) * 0.4f - time);
            Matrix4x4 m = Matrix4x4.identity;
            m.SetColumn(3, pos);
            matrices[index] = m;
        }
    }

    public NativeArray<Matrix4x4> matrices;
    public GraphicsBuffer colors = null;
    public int rows;
    public int columns;
    public Color color1;
    public Color color2;

    public RayTracingInstanceData(int _columns, int _rows, Color _color1, Color _color2)
    {
        rows = _rows;
        columns = _columns;
        color1 = _color1;
        color2 = _color2;
        
        matrices = new NativeArray<Matrix4x4>(rows * columns, Allocator.Persistent);        

        int index = 0;

        NativeArray<Vector3> data = new NativeArray<Vector3>(rows * columns, Allocator.Temp);

        Vector3 c1 = new Vector3(_color1.r, _color1.g, _color1.b);
        Vector3 c2 = new Vector3(_color2.r, _color2.g, _color2.b);

        for (int row = 0; row < rows; row++)
        {
            float z = row + 0.5f - rows * 0.5f;

            for (int column = 0; column < columns; column++)
            {
                float x = column + 0.5f - columns * 0.5f;

                matrices[index] = float4x4.Translate(new Vector3(x, 0, z));

                data[index] = Vector3.Lerp(c1, c2, math.sqrt(x * x + z * z) /  (0.5f * Math.Min(rows, columns)));

                index++;
            }
        }

        colors = new GraphicsBuffer(GraphicsBuffer.Target.Structured, rows * columns, 3 * sizeof(float));
        colors.SetData(data);
    }

    public void Dispose()
    {
        if (matrices.IsCreated)
        {
            matrices.Dispose();
        }

        if (colors != null)
        {
            colors.Release();
            colors = null;
        }
    }

    public void Update(float time)
    {
        AnimationJob jobData = new AnimationJob()
        {
            matrices = matrices,
            time = time
        };
        
        JobHandle handle = jobData.Schedule(matrices.Length, columns);

        handle.Complete();
    }
}