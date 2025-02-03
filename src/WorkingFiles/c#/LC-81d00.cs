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


/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public abstract class Script_Instance_81d00 : GH_ScriptInstance
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
  private void RunScript(object Model_in, List<string> beamId, ref object A)
  {
    var model = Model_in as Karamba.Models.Model;

    // Prepare the list of element IDs
    //List<string> elementsIDs = beamId.Cast<string>().ToList();  

    // Specify the load case you want to analyze
    string resultSelection = "LC6"; // Adjust according to your load case ID

    // Positions along the element (0.0 = start, 1.0 = end)
    List<double> ts = new List<double> { 0.0, 1.0 };

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
        resultSelection, // elementsGUIDs (not used in this case)
        0,
        0,
        out forces,
        out moments,
        out governingLoadCases,
        out governingLoadCaseInds,
        out elementInds
    );

    // Create lists to store the extracted force components
    List<double> N_list = new List<double>();
    List<double> Vy_list = new List<double>();
    List<double> Vz_list = new List<double>();
    List<double> Mt_list = new List<double>();
    List<double> My_list = new List<double>();
    List<double> Mz_list = new List<double>();

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

    // Assign the lists to the output parameters
    A = N_list;
    //Vy_out = Vy_list;
    //Vz_out = Vz_list;
    //Mt_out = Mt_list;
    //My_out = My_list;
    //Mz_out = Mz_list;
  }
  #endregion
  #region Additional

  #endregion
}