
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public class MeshLoaderDisk
{
    public IEnumerator LoadObjectFromDiskCR(string path, MeshLoader loader, GCodeHandler mc)
    {
        int layernum = 0;

        //create object to put all mesh types in
        var objectName = loader.GetObjectNameFromPath(path);

        var rootFolderOfObject = loader.dataPath + "/" + objectName;

        mc.RootForObject.transform.localPosition = Vector3.zero;
        mc.RootForObject.transform.localScale = Vector3.one;
        mc.RootForObject.transform.localRotation = Quaternion.identity;

        SortedDictionary<int, List<string>> sortedLayers = new SortedDictionary<int, List<string>>();

        foreach (var folder in Directory.GetDirectories(rootFolderOfObject))
        {
            var files = Directory.GetFiles(folder);
            foreach (var file in Directory.GetFiles(folder))
            {
                var l = Convert.ToInt32(file.Substring(file.LastIndexOf(@" ") + 1, file.LastIndexOf(".") - file.LastIndexOf(@" ") - 1));
                if (!sortedLayers.ContainsKey(l))
                {
                    sortedLayers.Add(l, new List<string>());
                    sortedLayers[l].Add(file);
                }
                else
                {
                    sortedLayers[l].Add(file);
                }
            }
        }


        foreach (var layers in sortedLayers)
        {
            foreach (var file in layers.Value)
            {
                var typeString = file.Substring(file.LastIndexOf(@"\") + 1, file.LastIndexOf(' ') - file.LastIndexOf(@"\") - 1);
                mc.CreateTypeObject(typeString);


                var layername = file.Substring(file.LastIndexOf(@"\") + 1, file.LastIndexOf(".") - file.LastIndexOf(@"\") - 1);

                //get mesh from file
                var mesh = MeshSerializer.DeserializeMesh(File.ReadAllBytes(file));


                //get the biggest layer number
                var l = mc.createLayerObject(layername, typeString, mesh);
                if (l > layernum)
                {
                    layernum = l;
                }

            }
            yield return null;
        }

        mc.endloading(layernum);
    }


}
