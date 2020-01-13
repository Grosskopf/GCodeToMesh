#region License and information
/* * * * *
 * A quick mesh serializer that allows to serialize a Mesh as byte array. It should
 * support any kind of mesh including skinned meshes, multiple submeshes, different
 * mesh topologies as well as blendshapes. I tried my best to avoid unnecessary data
 * by only serializing information that is present. It supports Vector4 UVs. The index
 * data may be stored as bytes, ushorts or ints depending on the actual highest used
 * vertex index within a submesh. It uses a tagging system for optional "chunks". The
 * base information only includes the vertex position array and the submesh count.
 * Everything else is handled through optional chunks.
 * 
 * 
 * The MIT License (MIT)
 * 
 * Copyright (c) 2018 Markus Göbel (Bunny83)
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 * * * * */
#endregion License and information

namespace B83.MeshTools
{
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using MeshDecimator;
    using MeshDecimator.Math;

    [System.Serializable]
    public class MeshData
    {
        private byte[] m_Data;
        private Mesh m_Mesh;
        public byte[] Data { get{ return m_Data; } }
        public void SetMesh(Mesh aMesh)
        {
            m_Mesh = aMesh;
            if (aMesh == null)
                m_Data = null;
            else
                m_Data = MeshSerializer.SerializeMesh(m_Mesh);
        }
    }


    public static class MeshSerializer
    {
        /*
         * Structure:
         * - Magic string "Mesh" (4 bytes)
         * - vertex count [int] (4 bytes)
         * - submesh count [int] (4 bytes)
         * - vertices [array of Vector3]
         * 
         * - additional chunks:
         *   [vertex attributes]
         *   - Name (name of the Mesh object)
         *   - Normals [array of Vector3]
         *   - Tangents [array of Vector4]
         *   - Colors [array of Color32]
         *   - UV0-4 [
         *       - component count[byte](2/3/4)
         *       - array of Vector2/3/4
         *     ]
         *   - BoneWeights [array of 4x int+float pair]
         *   
         *   [other data]
         *   - Submesh [
         *       - topology[byte]
         *       - count[int]
         *       - component size[byte](1/2/4)
         *       - array of byte/ushort/int
         *     ]
         *   - Bindposes [
         *       - count[int]
         *       - array of Matrix4x4
         *     ]
         *   - BlendShape [
         *       - Name [string]
         *       - frameCount [int]
         *       - frames [ array of:
         *           - frameWeight [float]
         *           - array of [
         *               - position delta [Vector3]
         *               - normal delta [Vector3]
         *               - tangent delta [Vector3]
         *             ]
         *         ]
         *     ]
         */
        private enum EChunkID : byte
        {
            End,
            Name,
            Normals,
            Tangents,
            Colors,
            BoneWeights,
            UV0, UV1, UV2, UV3,
            Submesh,
            Bindposes,
            BlendShape,
        }
        const uint m_Magic = 0x6873654D; // "Mesh"

        public static byte[] SerializeMesh(Mesh aMesh)
        {
            using (var stream = new MemoryStream())
            {
                SerializeMesh(stream, aMesh);
                return stream.ToArray();
            }
        }
        public static void SerializeMesh(MemoryStream aStream, Mesh aMesh)
        {
            using (var writer = new BinaryWriter(aStream))
                SerializeMesh(writer, aMesh);
        }
        public static void SerializeMesh(BinaryWriter aWriter, Mesh aMesh)
        {
            aWriter.Write(m_Magic);
            var vertices = aMesh.Vertices;
            int count = vertices.Length;
            int subMeshCount = 1;
            aWriter.Write(count);
            aWriter.Write(subMeshCount);
            foreach (var v in vertices)
                aWriter.WriteVector3((Vector3)v);

            // start of tagged chunks
            if (!string.IsNullOrEmpty(aMesh.name))
            {
                aWriter.Write((byte)EChunkID.Name);
                aWriter.Write(aMesh.name);
            }
            var normals = aMesh.Normals;
            if (normals != null && normals.Length == count)
            {
                aWriter.Write((byte)EChunkID.Normals);
                foreach (var v in normals)
                    aWriter.WriteVector3(v);
                normals = null;
            }
            var tangents = aMesh.Tangents;
            if (tangents != null && tangents.Length == count)
            {
                aWriter.Write((byte)EChunkID.Tangents);
                foreach (var v in tangents)
                    aWriter.WriteVector4(v);
                tangents = null;
            }

            List<int> indices = new List<int>(count * 3);
            for (int i = 0; i < subMeshCount; i++)
            {
                indices.Clear();
                aMesh.GetIndices(i, indices);
                if (indices.Count > 0)
                {
                    aWriter.Write((byte)EChunkID.Submesh);
                    //aWriter.Write((byte)aMesh.GetTopology(i));
                    aWriter.Write((byte)0);
                    aWriter.Write(indices.Count);
                    var max = indices.Max();
                    if (max < 256)
                    {
                        aWriter.Write((byte)1);
                        foreach (var index in indices)
                            aWriter.Write((byte)index);
                    }
                    else if (max < 65536)
                    {
                        aWriter.Write((byte)2);
                        foreach (var index in indices)
                            aWriter.Write((ushort)index);
                    }
                    else
                    {
                        aWriter.Write((byte)4);
                        foreach (var index in indices)
                            aWriter.Write(index);
                    }
                }
            }

            aWriter.Write((byte)EChunkID.End);
        }


    }


    public static class BinaryReaderWriterUnityExt
    {
        public static void WriteVector2(this BinaryWriter aWriter, Vector2 aVec)
        {
            aWriter.Write(aVec.x); aWriter.Write(aVec.y);
        }
        public static Vector2 ReadVector2(this BinaryReader aReader)
        {
            return new Vector2(aReader.ReadSingle(), aReader.ReadSingle());
        }
        public static void WriteVector3(this BinaryWriter aWriter, Vector3 aVec)
        {
            aWriter.Write(aVec.x); aWriter.Write(aVec.y); aWriter.Write(aVec.z);
        }
        public static Vector3 ReadVector3(this BinaryReader aReader)
        {
            return new Vector3(aReader.ReadSingle(), aReader.ReadSingle(), aReader.ReadSingle());
        }
        public static void WriteVector4(this BinaryWriter aWriter, Vector4 aVec)
        {
            aWriter.Write(aVec.x); aWriter.Write(aVec.y); aWriter.Write(aVec.z); aWriter.Write(aVec.w);
        }
        public static Vector4 ReadVector4(this BinaryReader aReader)
        {
            return new Vector4(aReader.ReadSingle(), aReader.ReadSingle(), aReader.ReadSingle(), aReader.ReadSingle());
        }


    }
}