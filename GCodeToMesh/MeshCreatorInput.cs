using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MeshDecimator.Math;

public class MeshCreatorInput
{
    public string meshname;
    public Vector3d[] newVertices;
    public Vector3[] newNormals;
    public Vector2[] newUV;
    public int[] newTriangles;
}
