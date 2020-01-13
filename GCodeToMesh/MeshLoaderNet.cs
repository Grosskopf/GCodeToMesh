using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using B83.MeshTools;
using MeshDecimator;

public class MeshLoaderNet
{

    private const string FolderToRawGCodes = "RawGCodes/";
    private string FolderToExportTo = "./";

    internal IEnumerator LoadObject(string urlToFile, GCodeHandler source, MeshLoader loader, String path = "")
    {
        if (path != "")
        {
            FolderToExportTo = path;
        }
        if (!source.loading)
        {
            source.loading = true;
            int startindex = urlToFile.LastIndexOf("/") + 1;
            string savePath = loader.dataPath + FolderToRawGCodes + urlToFile.Substring(startindex);

            if (!loader.CheckForExsitingObject(urlToFile))
            {
                if (!File.Exists(savePath))
                {
                    if (!Directory.Exists(loader.dataPath + "/RawGCodes/"))
                    {
                        Directory.CreateDirectory(loader.dataPath + "/RawGCodes/");
                    }
                    using (var client = new WebClient())
                    {
                        client.DownloadFile(urlToFile, savePath);
                    }


                }
                
                string[] Lines = File.ReadAllLines(savePath);
                loader.gcodeMeshGenerator.CreateObjectFromGCode(Lines, loader, source);

            }
            else
            {
                if (loader.loadingFromDisk == false)
                {
                    loader.path = urlToFile;
                    loader.loadingFromDisk = true;
                }
            }
        }
        return null;
    }

    public void SaveLayerAsAsset(Mesh mesh, string name)
    {
        if (!Directory.Exists(FolderToExportTo))
        {
            Directory.CreateDirectory(FolderToExportTo);
        }

        //Write the mesh to disk again
        mesh.name = name;
        File.WriteAllBytes(FolderToExportTo + name + ".mesh", MeshSerializer.SerializeMesh(mesh));// GOAAAL
    }
}