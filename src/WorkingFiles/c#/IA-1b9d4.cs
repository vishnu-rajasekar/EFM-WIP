using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Karamba.Models;
using KarambaCommon;
using Karamba.Elements;
using Karamba.Geometry;
using Karamba.Loads.Combination;
using Karamba.Utilities;
using Karamba.GHopper.Geometry;
using Karamba.Results;
using Karamba.Utilities.Mesher;
using Karamba.GHopper.Utilities.Mesher;
using Rhino.Geometry.Collections;
using System.IO;
using Rhino.Display;


/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public abstract class Script_Instance_1b9d4 : GH_ScriptInstance
{
  #region Utility functions
  /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
  /// <param name="text">String to print.</param>
  private void Print(string text) { /* Implementation hidden. */ }
  /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
  /// <param name="format">String format.</param>
  /// <param name="args">Formatting parameters.</param>
  private void Print(string format, params object[] args) { /* Implementation hidden. */ }
  /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj) { /* Implementation hidden. */ }
  /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
  /// <param name="obj">Object instance to parse.</param>
  private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }
  #endregion

  #region Members
  /// <summary>Gets the current Rhino document.</summary>
  private readonly RhinoDoc RhinoDocument;
  /// <summary>Gets the Grasshopper document that owns this script.</summary>
  private readonly GH_Document GrasshopperDocument;
  /// <summary>Gets the Grasshopper script component that owns this script.</summary>
  private readonly IGH_Component Component;
  /// <summary>
  /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
  /// Any subsequent call within the same solution will increment the Iteration count.
  /// </summary>
  private readonly int Iteration;
  #endregion
  /// <summary>
  /// This procedure contains the user code. Input parameters are provided as regular arguments,
  /// Output parameters as ref arguments. You don't have to assign output parameters,
  /// they will have a default value.
  /// </summary>
  #region Runscript
  private void RunScript(DataTree<Brep> F, int D, DataTree<Curve> B, ref object A, ref object M, ref object C)
  {
    // 1) Shift the beam tree if needed
    DataTree<Curve> wholeTree = TrimTree(B, 3);
    DataTree<Curve> beamTree = new DataTree<Curve>();


    // 1.1) Cull beam based on direction
    for (int i = 0; i < wholeTree.BranchCount; i++)
    {
      foreach (Curve beam in wholeTree.Branch(i))
      {
        Vector3d tan = beam.TangentAt(0.5);
        if (D == 1)
        {
          if (tan.X < -0.5 || tan.X > 0.5)  beamTree.Add(beam, new GH_Path(i));
          else                              beamTree.Add(null, new GH_Path(i));
        }
        else if (D == 2)
        {
          if (tan.Y < -0.5 || tan.Y > 0.5)  beamTree.Add(beam, new GH_Path(i));
          else                              beamTree.Add(null, new GH_Path(i));
        }
        else beamTree.Add(beam, new GH_Path(i));
      }
    }
    
    // 2) Create final output trees
    DataTree<Mesh> beamFaceMeshesTree = new DataTree<Mesh>();
    DataTree<double> beamFaceAreasTree = new DataTree<double>();
    DataTree<Mesh> beamColoredMeshesTree = new DataTree<Mesh>();

    // 3) Loop over branches
    for (int i = 0; i < beamTree.BranchCount; i++)
    {
      GH_Path path = F.Path(i);

      // (A) Mesh the first brep in this branch
      List<Brep> breps = F.Branch(i);
      if (breps == null || breps.Count < 1) continue;
      Brep mainBrep = breps[0];
      mainBrep.MergeCoplanarFaces(0.01);

      // Mesh the brep
      string warn, inf;
      List<Mesh> meshedBrepFaces = KarambaMesher(
          new List<Brep> { mainBrep },
          new List<Point3d>(), // no interior points
          0.2,                  // mResolution
          out warn,
          out inf
      );

      // (B) Break the big meshed brep(s) into face-level meshes
      List<Mesh> faceMeshes = new List<Mesh>();
      List<double> faceAreas = new List<double>();
      List<Point3d> centroids = new List<Point3d>();

      foreach (Mesh bigMesh in meshedBrepFaces)
      {
        // Break each "bigMesh" into single-face submeshes + area + centroid
        MeshFaceProperties(bigMesh, ref faceMeshes, ref faceAreas, ref centroids);
      }

      // (C) For each face centroid, find which beam is closest
      //     We keep the same beam indexing, including null beams
      List<Curve> beamList = beamTree.Branch(i);
      List<int> faceToBeamIndex = new List<int>(faceMeshes.Count);

      for (int face_i = 0; face_i < faceMeshes.Count; face_i++)
      {
        Point3d cPt = centroids[face_i];
        double bestDist = double.MaxValue;
        int bestBeam = -1;

        for (int bIdx = 0; bIdx < beamList.Count; bIdx++)
        {
          if (beamList[bIdx] != null)
          {
            double t;
            beamList[bIdx].ClosestPoint(cPt, out t);
            Point3d beamPt = beamList[bIdx].PointAt(t);
            double dist = cPt.DistanceTo(beamPt);

            if (dist < bestDist)
            {
              bestDist = dist;
              bestBeam = bIdx;
            }
          }
        }
        faceToBeamIndex.Add(bestBeam);
      }
      // (D) Create a color gradient for beams in this branch
      //     e.g. from Red to Blue, or any custom function you like
      List<ColorRGBA> beamColors = new List<ColorRGBA>();
      int beamCount = beamList.Count;
      for (int bIdx = 0; bIdx < beamCount; bIdx++)
      {
        double t = (beamCount == 1) ? 0.0 : bIdx / (beamCount - 1.0);
        ColorHSV hsv = new ColorHSV(t, 1.0, 1.0);
        ColorRGBA rgb = hsv.ToArgbColor();
        Print(rgb.ToString());
        beamColors.Add(rgb);
      }

      // (E) Group faces by beam, assign color, store in data trees
      for (int bIdx = 0; bIdx < beamCount; bIdx++)
      {
        GH_Path subPath = new GH_Path(path);
        subPath = subPath.AppendElement(bIdx);

        Curve beamRef = beamList[bIdx];
        if (beamRef == null)
        {
          // No valid beam => fill with "0" area & "null" mesh
          beamFaceAreasTree.Add(0.0, subPath);
          beamFaceMeshesTree.Add(null, subPath);
          beamColoredMeshesTree.Add(null, subPath);
          continue;
        }

        ColorRGBA groupColor = beamColors[bIdx];
        bool foundAnyFace = false;

        for (int face_i = 0; face_i < faceMeshes.Count; face_i++)
        {
          if (faceToBeamIndex[face_i] == bIdx)
          {
            foundAnyFace = true;
            // 1) Add to the uncolored final tree
            beamFaceMeshesTree.Add(faceMeshes[face_i], subPath);
            beamFaceAreasTree.Add(faceAreas[face_i], subPath);

            // 2) Make a copy (or reuse) and color it
            Mesh coloredMesh = faceMeshes[face_i].DuplicateMesh();
            coloredMesh.VertexColors.CreateMonotoneMesh((System.Drawing.Color)groupColor);

            // Add to the "colored" output
            beamColoredMeshesTree.Add(coloredMesh, subPath);
          }
        }
        if (!foundAnyFace)
        {
          beamFaceAreasTree.Add(0.0, subPath);
          beamFaceMeshesTree.Add(null, subPath);
          beamColoredMeshesTree.Add(null, subPath);
        }
      }
    }

    A = beamFaceAreasTree;
    M = beamTree;
    C = beamColoredMeshesTree;

  }
  #endregion
  #region Additional
  //--------------------------------------------------------------------------------
  // 2) KARAMBA MESHER (UNCHANGED)
  //--------------------------------------------------------------------------------
  private List<Mesh> KarambaMesher(
                              // A few mandatory parameters up front, e.g. mResolution
                              List<Brep> breps,
                              List<Point3d> incPoints,
                              double mResolution,
                              // out parameters cannot have defaults and they must be declared before the optional ones
                              out string warning,
                              out string info,
                              // Optional parameters with defaults:
                              int mMode = 0,
                              double refineEdgeResFactor = 0.67,
                              double cullDist = 0.01,
                              double tolerance = 0.001,
                              double smooth = 0,
                              int steps = 5)
  {
    // This list will be filled by the Karamba mesher
    List<Mesh> remappedMeshes = new List<Mesh>();
    // Call Karamba's MeshBrepsHeimrath solver
    Karamba.GHopper.Utilities.Mesher.MeshBrepsHeimrath.solve( breps,
                                                              incPoints,
                                                              mResolution,
                                                              mMode,
                                                              refineEdgeResFactor,
                                                              cullDist,
                                                              tolerance,
                                                              smooth,
                                                              steps,
                                                              out warning,
                                                              out info,
                                                              out remappedMeshes);

    return remappedMeshes;
  }
  //--------------------------------------------------------------------------------
  // 3) EXTRACT SINGLE-FACE SUBMESHES + AREA + CENTROID
  //--------------------------------------------------------------------------------
  private void MeshFaceProperties(Mesh m, ref List<Mesh> faceMeshes, ref List<double> faceAreas, ref List<Point3d> faceCentres)
  {
    MeshFaceList meshFaces = m.Faces;
    foreach (MeshFace face in meshFaces)
    {
      Mesh faceMesh = new Mesh();
      if (face.IsTriangle)
      {
        faceMesh.Vertices.Add(m.Vertices[face.A]);
        faceMesh.Vertices.Add(m.Vertices[face.B]);
        faceMesh.Vertices.Add(m.Vertices[face.C]);
        faceMesh.Faces.AddFace(0, 1, 2);
      }
      else
      {
        faceMesh.Vertices.Add(m.Vertices[face.A]);
        faceMesh.Vertices.Add(m.Vertices[face.B]);
        faceMesh.Vertices.Add(m.Vertices[face.C]);
        faceMesh.Vertices.Add(m.Vertices[face.D]);
        faceMesh.Faces.AddFace(0, 1, 2, 3);
      }
      faceMesh.Normals.ComputeNormals();
      faceMesh.Compact();
      faceMeshes.Add(faceMesh);
      var amProp = AreaMassProperties.Compute(faceMesh);
      faceAreas.Add(amProp.Area);
      faceCentres.Add(amProp.Centroid);
    }
  }
  //--------------------------------------------------------------------------------
  // 4) TRIMTREE
  //--------------------------------------------------------------------------------
  private DataTree<T> TrimTree<T>(DataTree<T> tree, int depth)
  {
    DataTree<T> trimTree = new DataTree<T>();
    for (int i = 0; i < tree.BranchCount; i++)
    {
      GH_Path path = new GH_Path(tree.Path(i));
      List<T> tree_list = tree.Branch(i);
      var shift = Math.Abs(depth);
      // Positive shift
      if (depth >= 0)
      {
        while (shift > 0 && path.Length > 1)
        {
          path = path.CullFirstElement();
          shift -= 1;
        }
      }
      // Negative shift
      else
      {
        while (shift > 0 && path.Length > 1)
        {
          path = path.CullElement();
          shift -= 1;
        }
      }
      foreach (var item in tree_list) trimTree.Add(item, path);
    }
    return trimTree;
  }
  // Simple linear interpolation between two System.Drawing.Colors
  private System.Drawing.Color HSVToColor(double hue, double sat, double val)
  {
    //Use Rhino's built-in if you prefer:
    ColorHSV colHsv = new ColorHSV(hue * 360.0, sat, val);
    return colHsv.ToArgbColor();

    //// Or do your own custom HSV->RGB math:
    //double H = hue * 360.0;
    //Rhino.Display.ColorHSV colHsv = new Rhino.Display.ColorHSV(H, sat, val);
    //return colHsv.ToArgbColor();
  }
  #endregion
}