using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System;
using System.Threading;
using System.Threading.Tasks;
using MeshDecimator.Math;
using MeshDecimator;
//using HoloToolkit.UX.Progress;

//using UnityMeshSimplifier.Scripts.UnityMeshSimplifier;

public class GCodeHandler
{

    public string[] names;
    [Serializable]
    public struct MaterialPreference
    {
        public string name;
        //public Material mat;
    }


    public MaterialPreference[] materialDictionary;
    public int layersvisible = 0;
    public float rotationclustersize = 0.0f;
    public float distanceclustersize = 0.0f;
    public bool loading = false;
    public int threadcount = Environment.ProcessorCount;
    public float quality = 0.5f;

    private int _layersvisible = 0;

    MeshLoader loader = new MeshLoader();
    void Start()
    {
        loader.Initialize();
    }

    /// <summary>
    /// call this before you recreate to regenerate with new clustersizes
    /// </summary>
    void clearchildren()
    {
        loader.Clear();
    }

    internal void CreateMesh(string meshname, Vector3d[] newVertices, Vector3[] newNormals, Vector2[] newUV, int[] newTriangles/*, Transform objectParent*/)
    {

        Mesh mesh = new Mesh(newVertices, newTriangles);
        string meshparentname = meshname.Split(' ')[0];
        mesh.Vertices = newVertices;
        mesh.Normals = newNormals;
        MeshSimplifierstruct msc = new MeshSimplifierstruct();
        msc.ToSimplify = mesh;
        msc.name = meshname;
        loader.meshSimplifierQueue.Add(msc);
        //}

        loader.simplifypossible = true;
    }

    internal IEnumerator LoadObject(string refs_download, string path_to)
    {
        IEnumerator enumerator = loader.LoadObjectFromNet(refs_download, this,path_to);
        Update();
        return enumerator;
    }

    internal IEnumerator LoadObjectFolder(string refs_download, string path_to)
    {
        //Console.WriteLine("before loading" + DateTimeOffset.Now);
        IEnumerator enumerator = loader.LoadObjectFromDisk(refs_download, this, path_to);
        //Console.WriteLine("after loading" + DateTimeOffset.Now);
        Update();
        return enumerator;
    }
    void printbounding(Vector3d[] arr)
    {
        double minx = double.MaxValue;
        double miny = double.MaxValue;
        double minz = double.MaxValue;
        double maxx = double.MinValue;
        double maxy = double.MinValue;
        double maxz = double.MinValue;
        foreach (Vector3d vec in arr)
        {
            if (vec.x < minx)
            {
                minx = vec.x;
            }
            if (vec.y < miny)
            {
                miny = vec.y;
            }
            if (vec.z < minz)
            {
                minz = vec.z;
            }
            if (vec.x > maxx)
            {
                maxx = vec.x;
            }
            if (vec.y > maxy)
            {
                maxy = vec.y;
            }
            if (vec.z > maxz)
            {
                maxz = vec.z;
            }
        }
    }

    public void Update()
    {
        loader.Update(this,threadcount,quality);
    }

    internal KeyValuePair<string, int> createLayerObjects(KeyValuePair<String, Mesh> KeyValuepPairLayer)
    {

        string parent = KeyValuepPairLayer.Key.Split(' ')[0];
        int layernum=Convert.ToInt32(KeyValuepPairLayer.Key.Substring(KeyValuepPairLayer.Key.LastIndexOf(" ") + 1));
        return new KeyValuePair<string, int>(parent,layernum);
    }

    internal void endloading(int layernum)
    {

        layersvisible = layernum;
        _layersvisible = layernum;
        loading = false;
    }

}
