using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MeshDecimator;

public class MeshLoader
    {

        public MeshLoaderNet meshLoaderNet = new MeshLoaderNet();
        public GCodeMeshGenerator gcodeMeshGenerator = new GCodeMeshGenerator();


        public float meshsimplifyquality = 0.5f;
        private bool issimplifying = false;
        public bool simplifypossible = false;
        public List<MeshSimplifierstruct> meshSimplifierQueue = new List<MeshSimplifierstruct>();
        private Queue<KeyValuePair<string, Mesh>> loadQueue = new Queue<KeyValuePair<string, Mesh>>();
        private object meshSimplifierQueueLock = new object();
        internal int simplifiedLayers;
        private object MeshCreatorInputQueueLock = new object();
        internal string dataPath;
        private bool _regenerateModel = false;
        internal bool filesLoadingfinished = false;
        internal string path;
        internal bool loadingFromDisk;
        private int EnqueuedMeshes;
        private int dequeuedMeshes = 0;

        private int layernum;


        public void simplyfyOne(int num)
        {

        Mesh ToSimplify = meshSimplifierQueue[num].ToSimplify;
        Mesh Simplified = MeshDecimator.MeshDecimation.DecimateMesh(ToSimplify, (int)(ToSimplify.VertexCount * meshsimplifyquality));
        lock (meshSimplifierQueueLock)
        {

            //Console.WriteLine("b " + num + " is " + DateTimeOffset.Now);
            meshSimplifierQueue[num].ToSimplify = Simplified;

            //.MeshSimplifier.SimplifyMesh(meshsimplifyquality);

            meshSimplifierQueue[num].simplified = true;
            simplifiedLayers++;
            }

            return;
        }

        internal void ToogleReload()
        {
            _regenerateModel = !_regenerateModel;
        }

        public IEnumerator LoadObjectFromNet(string urlToFile, GCodeHandler source,String path)
        {
            return meshLoaderNet.LoadObject(urlToFile, source, this,path);

        }

    public IEnumerator LoadObjectFromDisk(string savePath, GCodeHandler source, String path)
    {
        string[] Lines = File.ReadAllLines(savePath);
        gcodeMeshGenerator.CreateObjectFromGCode(Lines, this, source);
        return null;
        }
        internal void Initialize()
        {
            dataPath = System.AppDomain.CurrentDomain.BaseDirectory;
        }

        internal void Clear()
        {
            meshSimplifierQueue.Clear();
        }

        internal void Update(GCodeHandler source, int threadcount, float quality)
    {
        meshsimplifyquality = quality;
        gcodeMeshGenerator.Update(source, this);

        if (loadQueue.Count > 0 && loadingFromDisk)
            {

                KeyValuePair<string, Mesh> KeyValuepPairLayer = loadQueue.Dequeue();
                dequeuedMeshes++;

                KeyValuePair<String, int> LayerInfo = source.createLayerObjects(KeyValuepPairLayer);
                string parent = LayerInfo.Key;

                //get the biggest layer number

                var l = LayerInfo.Value;
                if (l > layernum)
                {
                    layernum = l;
                }

                dequeuedMeshes++;
                if (dequeuedMeshes == EnqueuedMeshes)
                {
                    source.endloading(layernum);
                    loadingFromDisk = false;
                    source.loading = false;
                }

            }

        List<Task> tasks = new List<Task>();
        int lastnum = 0;
        int lastsaved = 0;
        while (meshSimplifierQueue.Count > lastsaved)
            {
            if (meshSimplifierQueue[lastsaved].simplified)
            {
                //Console.WriteLine("a " + DateTimeOffset.Now);
                var layer = meshSimplifierQueue[lastsaved];
                Mesh destMesh = layer.ToSimplify;
                meshLoaderNet.SaveLayerAsAsset(destMesh, layer.name);
                lastsaved++;

                //lock (meshSimplifierQueueLock)
                //{
                //    meshSimplifierQueue.RemoveAt(0);
                //}
            }
            else
            {

                if (tasks.Count < threadcount && meshSimplifierQueue.Count > lastnum)
                {
                    int actnum = lastnum;
                    Task t = Task.Run(() => simplyfyOne(actnum));
                    lastnum++;
                    tasks.Add(t);
                }
                else
                {
                    if (tasks.Count > 0)
                    {
                        Task.WaitAny(tasks[0]);
                        tasks.RemoveAt(0);

                    }
                }
                //simplyfyOne();

            }
            }


        gcodeMeshGenerator.Update(source, this);


            if (gcodeMeshGenerator.createdLayers == simplifiedLayers && source.loading && filesLoadingfinished)
            {
                //source.StartCoroutine(closeProgress());
                filesLoadingfinished = false;
                simplifiedLayers = 0;
                gcodeMeshGenerator.createdLayers = 0;
                source.endloading(layernum);
            }

    }


        /// <summary>
        /// checks if there is already a model for the gcode
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        internal bool CheckForExsitingObject(string filename)
        {

            var objectName = GetObjectNameFromPath(filename);
            if (Directory.Exists(dataPath + "/" + objectName))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// extracts the name of the model
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal string GetObjectNameFromPath(string path)
        {
            int indexOfLastBackSlash = path.LastIndexOf(@"\");
            int indexOfLastForwardSlash = path.LastIndexOf(@"/");
            int indexOfFileExtention = path.LastIndexOf(".");

            if ((indexOfLastForwardSlash > 0 || indexOfLastBackSlash > 0) && indexOfFileExtention > 0)
            {
                int startIndex = indexOfLastBackSlash > 0 ? indexOfLastBackSlash : indexOfLastForwardSlash;

                return path.Substring(startIndex + 1, indexOfFileExtention - startIndex - 1);
            }
            else
            {
                return string.Empty;
            }
        }

    }
