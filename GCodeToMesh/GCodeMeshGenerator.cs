using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MeshDecimator.Math;

public class GCodeMeshGenerator
{

    private float plasticwidth = 0.6f;
    public int layercluster = 1;
    internal int createdLayers;

    private Queue<MeshCreatorInput> meshCreatorInputQueue = new Queue<MeshCreatorInput>();

    internal void CreateObjectFromGCode(string[] Lines, MeshLoader loader, GCodeHandler gcodeHandler)
    {
        if (Lines[0].Contains("Cura"))
        {
            CreateObjectFromGCodeCura(Lines, loader, gcodeHandler);
        }
        else if (Lines[0].Contains("Prusa"))
        {
            CreateObjectFromGCodePrusa(Lines, loader, gcodeHandler);
        }
    }

    internal void CreateObjectFromGCodeCura(string[] Lines, MeshLoader loader, GCodeHandler gcodeHandler)//takes ages and munches on all that juicy cpu, only use if absolutely necessary
    {




        //Read the text from directly from the test.txt file
        gcodeHandler.loading = true;
        loader.filesLoadingfinished = false;
        List<string> meshnames = new List<string>();
        int currentmesh = -1;
        Dictionary<string, List<List<Vector3>>> tmpmove = new Dictionary<string, List<List<Vector3>>>();
        Vector3 currpos = new Vector3(0, 0, 0);
        float accumulateddist = 0.0f;
        Vector3 lastpointcache = new Vector3(0, 0, 0);
        int linesread = 0;
        int layernum = 0;
        bool accumulating = false;
        float lastanglecache = 0.0f;
        float accumulatedangle = 0.0f;
        bool ismesh = false;
        foreach (string line in Lines)
        {
            linesread += 1;

            if (line.StartsWith(";TYPE:"))
            {
                ismesh = true;
                string namemesh = line.Substring(6) + " " + layernum.ToString("D8");
                if (line.Substring(6).Contains("WALL") || line.Substring(6).Contains("SKIN"))
                {
                    namemesh = "WALLS " + layernum.ToString("D8");
                }
                //here i change the type of 3d printed part i print next, this only works in cura-sliced stuff, slic3r doesnt have those comments
                if (!meshnames.Contains(namemesh))
                {
                    meshnames.Add(namemesh);
                    currentmesh = meshnames.Count - 1;
                    tmpmove[namemesh] = new List<List<Vector3>>();
                    tmpmove[namemesh].Add(new List<Vector3>());
                }
                else
                {
                    currentmesh = meshnames.FindIndex((namemesh).EndsWith);
                }
            }
            else if (line.StartsWith(";LAYER:"))
            {
                layernum = int.Parse(line.Substring(7));
                foreach (string namepart in tmpmove.Keys)
                {
                    createlayer(tmpmove[namepart], namepart, loader);
                }
                tmpmove.Clear();
                //todo create layer
            }
            else if ((line.StartsWith("G1") || line.StartsWith("G0")) && ((layernum % layercluster) == 0 || layercluster == 1))
            {
                //here i add a point to the list of visited points of the current part
                readG1Cura(gcodeHandler.distanceclustersize, gcodeHandler.rotationclustersize, line, ref accumulating, ref accumulateddist, ref accumulatedangle, ref meshnames, ref currentmesh, ref currpos, ref lastpointcache, ref lastanglecache, ref tmpmove, ref ismesh);
            }
            else if (line.StartsWith(";MESH:"))
            {
                ismesh = false;
            }
        }
        gcodeHandler.layersvisible = layernum;
        loader.filesLoadingfinished = true;
    }
    void readG1Cura(float distanceclustersize, float rotationclustersize, string line, ref bool accumulating, ref float accumulateddist, ref float accumulatedangle, ref List<string> meshnames, ref int currentmesh, ref Vector3 currpos, ref Vector3 lastpointcache, ref float lastanglecache, ref Dictionary<string, List<List<Vector3>>> tmpmove, ref bool ismesh)
    {
        string[] parts = line.Split(' ');

        if (accumulating)
        {
            accumulateddist += (currpos - lastpointcache).Magnitude;
            accumulatedangle += (float)Math.Atan2((currpos - lastpointcache).z, (currpos - lastpointcache).x - 1.0);
            //accumulatedangle += Mathf.Abs(lastanglecache - Vector2.Angle(new Vector2(1, 0), new Vector2((currpos - lastpointcache).x, (currpos - lastpointcache).z)));
        }
        lastpointcache = currpos;
        lastanglecache = (float)Math.Atan2((currpos - lastpointcache).z, (currpos - lastpointcache).x - 1.0);

        //lastanglecache = Vector2.Angle(new Vector2(1, 0), new Vector2((currpos - lastpointcache).x, (currpos - lastpointcache).z));

        if (!accumulating &&
            (line.Contains("X") || line.Contains("Y") || line.Contains("Z")) &&
            line.Contains("E") &&
            currpos != new Vector3(0, 0, 0)
            && currentmesh != -1)
        {
            string meshname = meshnames[currentmesh];
            if (tmpmove.ContainsKey(meshname))
            {

                tmpmove[meshname][tmpmove[meshname].Count - 1].Add(currpos);
            }
        }
        foreach (string part in parts)
        {
            if (part.StartsWith("X"))
            {
                currpos.x = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
            }
            else if (part.StartsWith("Y"))
            {
                currpos.z = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
            }
            else if (part.StartsWith("Z"))
            {
                currpos.y = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
            }
        }
        if (((!accumulating || accumulateddist > distanceclustersize || accumulatedangle > rotationclustersize) && (ismesh || line.Contains("E"))) && (line.Contains("X") || line.Contains("Y") || line.Contains("Z")) && currpos != new Vector3(0, 0, 0))
        {
            if (currentmesh != -1 && tmpmove.ContainsKey(meshnames[currentmesh]))
            {
                string meshname = meshnames[currentmesh];
                tmpmove[meshname][tmpmove[meshname].Count - 1].Add(currpos);
            }

            accumulateddist = 0.0f;
            accumulatedangle = 0.0f;
        }
        accumulating = true;
        if (line.Contains("E") &&
            (line.Contains("X") || line.Contains("Y") || line.Contains("Z")))
        {
            ismesh = true;
        }
        else
        {
            ismesh = false;
            accumulating = false;
            if (currentmesh != -1 && tmpmove.ContainsKey(meshnames[currentmesh]) && tmpmove[meshnames[currentmesh]][tmpmove[meshnames[currentmesh]].Count - 1].Count > 1)
            {
                tmpmove[meshnames[currentmesh]].Add(new List<Vector3>());
            }
        }
    }
    internal void CreateObjectFromGCodePrusa(string[] Lines, MeshLoader loader, GCodeHandler gcodeHandler)//takes ages and munches on all that juicy cpu, only use if absolutely necessary
    {
    
        //Read the text from directly from the test.txt file
        //StreamReader reader = new StreamReader(new FileStream(filename, FileMode.Open));
        gcodeHandler.loading = true;
        loader.filesLoadingfinished = false;
        //mc.print("loading " + filename);
        List<string> meshnames = new List<string>();
        int currentmesh = -1;
        Dictionary<string, List<List<Vector3>>> tmpmove = new Dictionary<string, List<List<Vector3>>>();
        Vector3 currpos = new Vector3(0, 0, 0);
        float accumulateddist = 0.0f;
        Vector3 lastpointcache = new Vector3(0, 0, 0);
        int linesread = 0;
        int layernum = -1;
        bool accumulating = false;
        float lastanglecache = 0.0f;
        float accumulatedangle = 0.0f;
        bool ismesh = false;
        //bool islayerheight = false;
        foreach (string line in Lines)
        {
            //Layerheigt is defined in Prusa by writing ";AFTER_LAYER_CHANGE" and in the next line writing the height, therefore this happens:
            linesread += 1;
            if (line.Contains("support"))
            {
                bool ishere = true;
            }
            bool isnotmeshmove = line.Contains("wipe and retract") || line.Contains("move to first") || line.Contains("move inwards before travel") || line.Contains("retract") || line.Contains("lift Z") || line.Contains("move to first perimeter point") || line.Contains("restore layer Z") || line.Contains("unretract") || line.Contains("Move") || line.Contains("home");
            if (line.Contains("move to next layer"))
            {
                layernum = layernum + 1;
                currpos.y = float.Parse(line.Split('Z')[1].Split(' ')[0], CultureInfo.InvariantCulture.NumberFormat);
                //islayerheight = true;
                foreach (string namepart in tmpmove.Keys)
                {
                    createlayer(tmpmove[namepart], namepart, loader);
                }
                tmpmove.Clear();
                //todo create layer
            }
            //movement commands are all G0 or G1 in Prusa Gcode

            else if ((line.StartsWith("G1") || line.StartsWith("G0")) && layernum != -1 && ((layernum % layercluster) == 0 || layercluster == 1))
            {
                //bool isnew = false;
                if (line.Contains(";")&&!isnotmeshmove)
                {

                    string namemesh = line.Split(';')[1].Split(' ')[1]+ " " + layernum.ToString(CultureInfo.InvariantCulture);//In Prusaslicer the comments about what the Line Means are right next to the line

                    if (!meshnames.Contains(namemesh))
                    {
                        //isnew = true;
                        meshnames.Add(namemesh);
                        currentmesh = meshnames.Count - 1;
                        tmpmove[namemesh] = new List<List<Vector3>>();
                        tmpmove[namemesh].Add(new List<Vector3>());
                    }
                    else
                    {
                        if (meshnames[currentmesh] != namemesh||!ismesh)//Sometimes a type like infill happens more often inside one layer
                        {
                            tmpmove[namemesh].Add(new List<Vector3>());
                            //Console.WriteLine("after layer" + layernum + DateTimeOffset.Now);
                            //isnew = true;
                        }
                        //currentmesh = meshnames.FindIndex((namemesh).EndsWith);
                    }
                    ismesh = true;
                }
                string[] parts = line.Split(';')[0].Split(' ');
                if (line.Contains(";") && !isnotmeshmove)
                {
                    //if (accumulating)
                    //{
                    //    accumulateddist += (currpos - lastpointcache).Magnitude;
                    //    accumulatedangle += (float)Math.Atan2((currpos - lastpointcache).z, (currpos - lastpointcache).x - 1.0);
                    //    //accumulatedangle += Mathf.Abs(lastanglecache - Vector2.Angle(new Vector2(1, 0), new Vector2((currpos - lastpointcache).x, (currpos - lastpointcache).z)));
                    //}
                    //lastpointcache = currpos;
                    //lastanglecache = (float)Math.Atan2((currpos - lastpointcache).z, (currpos - lastpointcache).x - 1.0);
                    //lastanglecache = Vector2.Angle(new Vector2(1, 0), new Vector2((currpos - lastpointcache).x, (currpos - lastpointcache).z));

                    //Since The G1 or G0 Commands are just "go to" commands, we need to store the Previous position as well, so before we touch currpos, we add it to the mesh, but only once per mesh
                    if (!accumulating &&
                        (line.Contains("X") || line.Contains("Y") || line.Contains("Z")) &&
                        line.Contains("E") &&
                        currpos.x != 0 && currpos.z != 0
                        && currentmesh != -1)
                    {
                        string meshname = meshnames[currentmesh];
                        if (tmpmove.ContainsKey(meshname))
                        {

                            tmpmove[meshname][tmpmove[meshname].Count - 1].Add(currpos);
                        }

                    }

                    //now we can update currpos
                    foreach (string part in parts)
                    {
                        if (part.Length > 0 && part[0] == 'X')
                        {
                            currpos.x = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                        }
                        else if (part.Length > 0 && part[0] == 'Y')
                        {
                            currpos.z = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat); //Unity has a Lefthanded Coordinate System (Y up), Gcode a Righthanded (Z up)
                        }
                        else if (part.Length > 0 && part[0] == 'Z')
                        {
                            currpos.y = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                        }
                    }
                    if (((!accumulating || accumulateddist > gcodeHandler.distanceclustersize || accumulatedangle > gcodeHandler.rotationclustersize) && (ismesh || line.Contains("E"))) && (line.Contains("X") || line.Contains("Y") || line.Contains("Z")) && currpos != new Vector3(0, 0, 0))
                    {
                        if (currentmesh != -1 /*&& tmpmove.ContainsKey(meshnames[currentmesh])*/)
                        {
                            string meshname = meshnames[currentmesh];
                            tmpmove[meshname][tmpmove[meshname].Count - 1].Add(currpos);
                        }

                        //accumulateddist = 0.0f;
                        //accumulatedangle = 0.0f;
                        //accumulating = true;
                    }
                }
                else
                {
                    foreach (string part in parts)
                    {
                        if (part.Length>0 && part[0] == 'X')
                        {
                            currpos.x = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                        }
                        else if (part.Length > 0 && part[0] == 'Y')
                        {
                            currpos.z = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat); //Unity has a Lefthanded Coordinate System (Y up), Gcode a Righthanded (Z up)
                        }
                        else if (part.Length > 0 && part[0] == 'Z')
                        {
                            if(part.Length>1)
                                currpos.y = float.Parse(part.Substring(1), CultureInfo.InvariantCulture.NumberFormat);
                        }
                    }
                }
            }
            if (line.StartsWith(";BEFORE-LAYER-CHANGE")||line.Contains("retract"))
            {
                ismesh = false;
            }
        }
        gcodeHandler.layersvisible = layernum;
        loader.filesLoadingfinished = true;
    }
    void createlayer(List<List<Vector3>> tmpmoves, string meshname, MeshLoader loader)
    {
        List<Vector3d> newVertices = new List<Vector3d>();
        List<Vector3> newNormals = new List<Vector3>();
        List<Vector2> newUV = new List<Vector2>();
        List<int> newTriangles = new List<int>();
        List<Dictionary<int, Dictionary<int, int>>> neighbours = new List<Dictionary<int, Dictionary<int, int>>>();
        for (int tmpmvn = 0; tmpmvn < tmpmoves.Count; tmpmvn++)
        {
            List<Vector3> tmpmove = tmpmoves[tmpmvn];
            
            if (tmpmove.Count > 1)
            {
                createMesh(ref newVertices,ref tmpmove,ref newNormals,ref newUV,ref newTriangles);
            }
        }
        MeshCreatorInput mci = new MeshCreatorInput
        {
            meshname = meshname,
            newUV = newUV.ToArray(),
            newNormals = newNormals.ToArray(),
            newVertices = newVertices.ToArray(),
            newTriangles = newTriangles.ToArray()
        };
        meshCreatorInputQueue.Enqueue(mci);
        createdLayers++;
    }

    internal void createMesh(ref List<Vector3d> newVertices, ref List<Vector3> tmpmove, ref List<Vector3> newNormals, ref List<Vector2> newUV, ref List<int> newTriangles)
    {

        //here i generate the mesh from the tmpmove list, wich is a list of points the extruder goes to
        int vstart = newVertices.Count;
        Vector3 dv = tmpmove[1] - tmpmove[0];
        Vector3 dvt = dv; dvt.x = dv.z; dvt.z = -dv.x;
        dvt = -dvt.Normalized;
        newVertices.Add(tmpmove[0] - dv.Normalized * 0.5f + dvt * plasticwidth * 0.5f);
        newVertices.Add(tmpmove[0] - dv.Normalized * 0.5f - dvt * 0.5f * plasticwidth);
        newVertices.Add(tmpmove[0] - dv.Normalized * 0.5f - dvt * 0.5f * plasticwidth - new Vector3(0, -0.25f, 0) * layercluster);
        newVertices.Add(tmpmove[0] - dv.Normalized * 0.5f + dvt * plasticwidth * 0.5f - new Vector3(0, -0.25f, 0) * layercluster);
        newNormals.Add((dvt.Normalized * plasticwidth / 2 + new Vector3(0, plasticwidth / 2, 0) - dv.Normalized * plasticwidth / 2).Normalized);
        newNormals.Add((dvt.Normalized * -plasticwidth / 2 + new Vector3(0, plasticwidth / 2, 0) - dv.Normalized * plasticwidth / 2).Normalized);
        newNormals.Add((dvt.Normalized * -plasticwidth / 2 + new Vector3(0, -plasticwidth / 2, 0) - dv.Normalized * plasticwidth / 2).Normalized);
        newNormals.Add((dvt.Normalized * plasticwidth / 2 + new Vector3(0, -plasticwidth / 2, 0) - dv.Normalized * plasticwidth / 2).Normalized);
        newUV.Add(new Vector2(0.0f, 0.0f));
        newUV.Add(new Vector2(0.0f, 1.0f));
        newUV.Add(new Vector2(1.0f, 1.0f));
        newUV.Add(new Vector2(1.0f, 0.0f));

        newTriangles.Add(vstart + 2);
        newTriangles.Add(vstart + 1);
        newTriangles.Add(vstart + 0); //back (those need to be in clockwise orientation for culling to work right)
        newTriangles.Add(vstart + 0);
        newTriangles.Add(vstart + 3);
        newTriangles.Add(vstart + 2);


        for (int i = 1; i < tmpmove.Count - 1; i++)
        {

            Vector3 dv1 = tmpmove[i] - tmpmove[i - 1];
            Vector3 dvt1 = dv1; dvt1.x = dv1.z; dvt1.z = -dv1.x;
            Vector3 dv2 = tmpmove[i + 1] - tmpmove[i];
            Vector3 dvt2 = dv2; dvt2.x = dv2.z; dvt2.z = -dv2.x;
            dvt = (dvt1 + dvt2).Normalized * -plasticwidth;
            newVertices.Add(tmpmove[i] + dvt * 0.5f);
            newVertices.Add(tmpmove[i] - dvt * 0.5f);
            newVertices.Add(tmpmove[i] - dvt * 0.5f - new Vector3(0, -0.25f, 0) * layercluster);
            newVertices.Add(tmpmove[i] + dvt * 0.5f - new Vector3(0, -0.25f, 0) * layercluster);
            newNormals.Add((dvt.Normalized + new Vector3(0, 0.125f, 0)).Normalized);
            newNormals.Add((dvt.Normalized + new Vector3(0, 0.125f, 0)).Normalized);
            newNormals.Add((dvt.Normalized + new Vector3(0, -0.125f, 0)).Normalized);
            newNormals.Add((dvt.Normalized + new Vector3(0, -0.125f, 0)).Normalized);
            newUV.Add(new Vector2(0.0f, 0.0f));
            newUV.Add(new Vector2(0.0f, 1.0f));
            newUV.Add(new Vector2(1.0f, 1.0f));
            newUV.Add(new Vector2(1.0f, 0.0f));

            newTriangles.Add(vstart + 0 + 4 * (i - 1));
            newTriangles.Add(vstart + 1 + 4 * (i - 1));
            newTriangles.Add(vstart + 5 + 4 * (i - 1)); //top
            newTriangles.Add(vstart + 0 + 4 * (i - 1));
            newTriangles.Add(vstart + 5 + 4 * (i - 1));
            newTriangles.Add(vstart + 4 + 4 * (i - 1));

            newTriangles.Add(vstart + 1 + 4 * (i - 1));
            newTriangles.Add(vstart + 2 + 4 * (i - 1));
            newTriangles.Add(vstart + 6 + 4 * (i - 1));//left
            newTriangles.Add(vstart + 1 + 4 * (i - 1));
            newTriangles.Add(vstart + 6 + 4 * (i - 1));
            newTriangles.Add(vstart + 5 + 4 * (i - 1));

            newTriangles.Add(vstart + 0 + 4 * (i - 1));
            newTriangles.Add(vstart + 4 + 4 * (i - 1));
            newTriangles.Add(vstart + 3 + 4 * (i - 1));//right
            newTriangles.Add(vstart + 3 + 4 * (i - 1));
            newTriangles.Add(vstart + 4 + 4 * (i - 1));
            newTriangles.Add(vstart + 7 + 4 * (i - 1));

            newTriangles.Add(vstart + 2 + 4 * (i - 1));
            newTriangles.Add(vstart + 3 + 4 * (i - 1));
            newTriangles.Add(vstart + 7 + 4 * (i - 1));//bottom
            newTriangles.Add(vstart + 2 + 4 * (i - 1));
            newTriangles.Add(vstart + 7 + 4 * (i - 1));
            newTriangles.Add(vstart + 6 + 4 * (i - 1));
        }

        dv = tmpmove[tmpmove.Count - 1] - tmpmove[tmpmove.Count - 2];
        dvt = dv; dvt.x = dv.z; dvt.z = -dv.x;
        dvt = dvt.Normalized * plasticwidth;
        dv = dv.Normalized * plasticwidth / 2;
        int maxi = tmpmove.Count - 2;

        newVertices.Add(tmpmove[maxi] + dv + dvt * 0.5f);
        newVertices.Add(tmpmove[maxi] + dv - dvt * 0.5f);
        newVertices.Add(tmpmove[maxi] + dv - dvt * 0.5f - new Vector3(0, -0.25f, 0) * layercluster);
        newVertices.Add(tmpmove[maxi] + dv + dvt * 0.5f - new Vector3(0, -0.25f, 0) * layercluster);
        newNormals.Add((dvt + new Vector3(0, plasticwidth / 2, 0) + dv).Normalized);
        newNormals.Add((-dvt + new Vector3(0, plasticwidth / 2, 0) + dv).Normalized);
        newNormals.Add((-dvt + new Vector3(0, -plasticwidth / 2, 0) + dv).Normalized);
        newNormals.Add((dvt + new Vector3(0, -plasticwidth / 2, 0) + dv).Normalized);
        newUV.Add(new Vector2(0.0f, 0.0f));
        newUV.Add(new Vector2(0.0f, 1.0f));
        newUV.Add(new Vector2(1.0f, 1.0f));
        newUV.Add(new Vector2(1.0f, 0.0f));

        newTriangles.Add(vstart + 0 + 4 * maxi);
        newTriangles.Add(vstart + 1 + 4 * maxi);
        newTriangles.Add(vstart + 5 + 4 * maxi); //top
        newTriangles.Add(vstart + 0 + 4 * maxi);
        newTriangles.Add(vstart + 5 + 4 * maxi);
        newTriangles.Add(vstart + 4 + 4 * maxi);

        newTriangles.Add(vstart + 1 + 4 * maxi);
        newTriangles.Add(vstart + 2 + 4 * maxi);
        newTriangles.Add(vstart + 6 + 4 * maxi);//left
        newTriangles.Add(vstart + 1 + 4 * maxi);
        newTriangles.Add(vstart + 6 + 4 * maxi);
        newTriangles.Add(vstart + 5 + 4 * maxi);

        newTriangles.Add(vstart + 0 + 4 * maxi);
        newTriangles.Add(vstart + 4 + 4 * maxi);
        newTriangles.Add(vstart + 3 + 4 * maxi);//right
        newTriangles.Add(vstart + 3 + 4 * maxi);
        newTriangles.Add(vstart + 4 + 4 * maxi);
        newTriangles.Add(vstart + 7 + 4 * maxi);

        newTriangles.Add(vstart + 2 + 4 * maxi);
        newTriangles.Add(vstart + 3 + 4 * maxi);
        newTriangles.Add(vstart + 7 + 4 * maxi);//bottom
        newTriangles.Add(vstart + 2 + 4 * maxi);
        newTriangles.Add(vstart + 7 + 4 * maxi);
        newTriangles.Add(vstart + 6 + 4 * maxi);

        newTriangles.Add(vstart + 4 + 4 * maxi);
        newTriangles.Add(vstart + 5 + 4 * maxi);
        newTriangles.Add(vstart + 7 + 4 * maxi);//front
        newTriangles.Add(vstart + 7 + 4 * maxi);
        newTriangles.Add(vstart + 5 + 4 * maxi);
        newTriangles.Add(vstart + 6 + 4 * maxi);

    }
    internal void Update(GCodeHandler source, MeshLoader loader)
    {

        while (meshCreatorInputQueue.Count > 0)
        {
            MeshCreatorInput mci = meshCreatorInputQueue.Dequeue();
            source.CreateMesh(mci.meshname, mci.newVertices, mci.newNormals, mci.newUV, mci.newTriangles/*, source.RootForObject.transform*/);
        }

    }
}