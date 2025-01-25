using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using static UnityEngine.Mathf;

public static class ShaderHelper
{
    public enum DepthMode { None = 0, Depth16 = 16, Depth24 = 24 }
    public static readonly GraphicsFormat RGBA_SFloat = GraphicsFormat.R32G32B32A32_SFloat;

    #region ComputeShaders
    /// Convenience method for dispatching a compute shader.
    /// It calculates the number of thread groups based on the number of iterations needed.
    public static void Dispatch(ComputeShader cs, int numIterationsX, int numIterationsY = 1, int numIterationsZ = 1, int kernelIndex = 0)
    {
        Vector3Int threadGroupSizes = GetThreadGroupSizes(cs, kernelIndex);
        int numGroupsX = Mathf.CeilToInt(numIterationsX / (float)threadGroupSizes.x);
        int numGroupsY = Mathf.CeilToInt(numIterationsY / (float)threadGroupSizes.y);
        int numGroupsZ = Mathf.CeilToInt(numIterationsZ / (float)threadGroupSizes.y);
        cs.Dispatch(kernelIndex, numGroupsX, numGroupsY, numGroupsZ);
    }

    public static Vector3Int GetThreadGroupSizes(ComputeShader compute, int kernelIndex = 0)
    {
        uint x, y, z;
        compute.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);
        return new Vector3Int((int)x, (int)y, (int)z);
    }

    // Read data in append buffer to array
    // Note: this is very slow as it reads the data from the GPU to the CPU
    public static T[] ReadDataFromBuffer<T>(ComputeBuffer buffer, bool isAppendBuffer)
    {
        int numElements = buffer.count;
        if (isAppendBuffer)
        {
            // Get number of elements in append buffer
            ComputeBuffer sizeBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.IndirectArguments);
            ComputeBuffer.CopyCount(buffer, sizeBuffer, 0);
            int[] bufferCountData = new int[1];
            sizeBuffer.GetData(bufferCountData);
            numElements = bufferCountData[0];
            Release(sizeBuffer);
        }

        // Read data from append buffer
        T[] data = new T[numElements];
        buffer.GetData(data);

        return data;

    }
    #endregion

    #region Create Buffers

    public static ComputeBuffer CreateAppendBuffer<T>(int capacity)
    {
        int stride = GetStride<T>();
        ComputeBuffer buffer = new ComputeBuffer(capacity, stride, ComputeBufferType.Append);
        buffer.SetCounterValue(0);
        return buffer;

    }

    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, int count)
    {
        count = Mathf.Max(1, count); // cannot create 0 length buffer
        int stride = GetStride<T>();
        bool createNewBuffer = buffer == null || !buffer.IsValid() || buffer.count != count || buffer.stride != stride;
        if (createNewBuffer)
        {
            Release(buffer);
            buffer = new ComputeBuffer(count, stride, ComputeBufferType.Structured);
        }
    }

    public static ComputeBuffer CreateStructuredBuffer<T>(T[] data)
    {
        var buffer = new ComputeBuffer(data.Length, GetStride<T>());
        buffer.SetData(data);
        return buffer;
    }

    public static ComputeBuffer CreateStructuredBuffer<T>(List<T> data) where T : struct
    {
        var buffer = new ComputeBuffer(data.Count, GetStride<T>());
        buffer.SetData<T>(data);
        return buffer;
    }
    public static int GetStride<T>() => System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));

    public static ComputeBuffer CreateStructuredBuffer<T>(int count)
    {
        return new ComputeBuffer(count, GetStride<T>());
    }


    // Create a compute buffer containing the given data (Note: data must be blittable)
    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, T[] data) where T : struct
    {
        // Cannot create 0 length buffer (not sure why?)
        int length = Max(1, data.Length);
        // The size (in bytes) of the given data type
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));

        // If buffer is null, wrong size, etc., then we'll need to create a new one
        if (buffer == null || !buffer.IsValid() || buffer.count != length || buffer.stride != stride)
        {
            if (buffer != null) { buffer.Release(); }
            buffer = new ComputeBuffer(length, stride, ComputeBufferType.Structured);
        }

        buffer.SetData(data);
    }

    // Create a compute buffer containing the given data (Note: data must be blittable)
    public static void CreateStructuredBuffer<T>(ref ComputeBuffer buffer, List<T> data) where T : struct
    {
        // Cannot create 0 length buffer (not sure why?)
        int length = Max(1, data.Count);
        // The size (in bytes) of the given data type
        int stride = System.Runtime.InteropServices.Marshal.SizeOf(typeof(T));

        // If buffer is null, wrong size, etc., then we'll need to create a new one
        if (buffer == null || !buffer.IsValid() || buffer.count != length || buffer.stride != stride)
        {
            if (buffer != null) { buffer.Release(); }
            buffer = new ComputeBuffer(length, stride, ComputeBufferType.Structured);
        }

        buffer.SetData(data);
    }


    #endregion


    public static void InitMaterial(Shader shader, ref Material mat)
    {
        if (mat == null || (mat.shader != shader && shader != null))
        {
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Texture");
            }

            mat = new Material(shader);
        }
    }

    public static ComputeBuffer CreateArgsBuffer(Mesh mesh, int numInstances)
    {
        const int subMeshIndex = 0;
        uint[] args = new uint[5];
        args[0] = (uint)mesh.GetIndexCount(subMeshIndex);
        args[1] = (uint)numInstances;
        args[2] = (uint)mesh.GetIndexStart(subMeshIndex);
        args[3] = (uint)mesh.GetBaseVertex(subMeshIndex);
        args[4] = 0; // offset

        ComputeBuffer argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
        return argsBuffer;
    }

    // Create args buffer for instanced indirect rendering (number of instances comes from size of append buffer)
    public static ComputeBuffer CreateArgsBuffer(Mesh mesh, ComputeBuffer appendBuffer)
    {
        ComputeBuffer argsBuffer = CreateArgsBuffer(mesh, 0);
        SetArgsBufferCount(argsBuffer, appendBuffer);
        return argsBuffer;
    }

    public static void SetArgsBufferCount(ComputeBuffer argsBuffer, ComputeBuffer appendBuffer)
    {
        ComputeBuffer.CopyCount(appendBuffer, argsBuffer, sizeof(uint));
    }

    public static void Release(ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            buffer.Release();
        }
    }

    /// Releases supplied buffer/s if not null
    public static void Release(params ComputeBuffer[] buffers)
    {
        for (int i = 0; i < buffers.Length; i++)
        {
            Release(buffers[i]);
        }
    }

    public static void Release(RenderTexture tex)
    {
        if (tex != null)
        {
            tex.Release();
        }
    }

}