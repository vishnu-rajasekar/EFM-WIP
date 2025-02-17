using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Karamba.Models;
using KarambaCommon;
using Karamba.Elements;
using Karamba.Geometry;
using Karamba.Loads.Combination;
using feb;
using Karamba.GHopper.Geometry;
using Karamba.Results;
using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;


/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public abstract class Script_Instance_88d71 : GH_ScriptInstance
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
  private void RunScript(object Model_in, List<string> beamId, double L, object stone_thk, object h_eff, ref object A)
  {
    var methods = typeof(Karamba.Results.BeamForces).GetMethods();
    foreach (var m in methods)
    {
      Print(m.ToString());
    }
    var model = Model_in as Karamba.Models.Model;

    // Positions along the element (0.0 = start, 1.0 = end)
    List<double> ts = new List<double> { 0.0, 1.0 };


    // Create lists to store the extracted force components
    List<double> N_list_1 = new List<double>();
    List<double> Vy_list_1 = new List<double>();
    List<double> Vz_list_1 = new List<double>();
    List<double> Mt_list_1 = new List<double>();
    List<double> My_list_1 = new List<double>();
    List<double> Mz_list_1 = new List<double>();
    KarBeamForces(Model_in, beamId, "LC1", ts, ref N_list_1, ref Vy_list_1, ref Vz_list_1, ref Mt_list_1, ref My_list_1, ref Mz_list_1);


    List<Point3> positions = new List<Point3>();

    KarNodalDisp(Model_in, beamId, "LC1", ref positions);

    A = positions;
   
  }
  #endregion
  #region Additional
  private List<int> BeamNodalInd(object Model_in, List<string> beamId)
  {
    var model = Model_in as Karamba.Models.Model;

    List<int> nodeIndices = new List<int>();


    // Access the beam element using its index
    List<ModelElement> beams = model.elementsByID(beamId);
    foreach (ModelElement beam in beams)
    {
      // Extract the start and end node indices
      nodeIndices.AddRange(beam.node_inds);

    }
    
    return nodeIndices;
  }

  //--------------------------------------------------------------------------------
  // 1) Calculate Node Displacements
  //--------------------------------------------------------------------------------
  private  void KarNodalDisp(object Model_in, List<string> beamId, string resultSelection, ref List<double> ys)
  {

    var model = Model_in as Karamba.Models.Model;
    List<int> nodeIndices = BeamNodalInd(Model_in, beamId);

    List<List<Vector3>> trans;
    List<List<Vector3>> rotat;
    List<Point3> positions;
    List<List<Karamba.Loads.Combination.LoadCase>> governingLoadCases;
    List<List<int>> governingLoadCaseInds;

    NodeDisplacements.solve(
     model,
     resultSelection,
     nodeIndices,
     out trans,
     out rotat,
     out governingLoadCases,
     out governingLoadCaseInds,
     out positions
    );

    for (int i = 0; i < trans.Count; i++)
    {
      for (int j = 0; j < trans[i].Count; j++)
      {
        // Access the Y component of the translation vector
        double yComponent = trans[i][j].Y;
        ys.Add(yComponent);
      }
    }


  }
  //--------------------------------------------------------------------------------
  // 2) Calculate Beam Forces
  //--------------------------------------------------------------------------------
  private void KarBeamForces(object Model_in, List<string> beamId, string resultSelection, List<double> ts, ref List<double> N_list,
                                                                                              ref List<double> Vy_list,
                                                                                              ref List<double> Vz_list,
                                                                                              ref List<double> Mt_list,
                                                                                              ref List<double> My_list,
                                                                                              ref List<double> Mz_list)
  {
    var model = Model_in as Karamba.Models.Model;

    // Positions along the element (0.0 = start, 1.0 = end)
    // Output variables for the solve method
    List<List<List<Vector3>>> forces;
    List<List<List<Vector3>>> moments;
    List<List<List<Karamba.Loads.Combination.LoadCase>>> governingLoadCases;
    List<List<List<int>>> governingLoadCaseInds;
    List<int> elementInds;

    // Call the solve method
    BeamForces.solve(
      model,
      beamId,
      resultSelection,
      0,
      0,
      out forces,
      out moments,
      out governingLoadCases,
      out governingLoadCaseInds,
      out elementInds
    );

    // Iterate over the nested lists to extract force components
    for (int i = 0; i < forces.Count; i++)
    {
      // For each element
      for (int j = 0; j < forces[i].Count; j++)
      {
        // For each position along the element (ts)
        // Assuming only one load case, so index 0
        Vector3 forceVec = forces[i][j][0];
        Vector3 momentVec = moments[i][j][0];

        // Axial force N (local x-direction)
        N_list.Add(forceVec.X);

        // Shear forces Vy and Vz (local y and z-directions)
        Vy_list.Add(forceVec.Y);
        Vz_list.Add(forceVec.Z);

        // Torsional moment Mt (around local x-axis)
        Mt_list.Add(momentVec.X);

        // Bending moments My and Mz (around local y and z-axes)
        My_list.Add(momentVec.Y);
        Mz_list.Add(momentVec.Z);
      }
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
  class T
  { }
  #endregion
}