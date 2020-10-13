﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using ConnectorGrashopper.Extras;
using GH_IO.Serialization;
using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Speckle.Core.Kits;
using Speckle.Core.Models;

namespace ConnectorGrashopper.Objects
{
  // TODO: Convert to task capable component / async so as to not block the ffffing ui
  public class ExpandSpeckleObject : GH_Component, IGH_VariableParameterComponent
  {
    public override Guid ComponentGuid => new Guid("C3BC3130-97C9-4DDE-9D4F-7A7FB82F7F2E");

    protected override Bitmap Icon  => null; 

    private ISpeckleConverter Converter;

    private ISpeckleKit Kit;

    public ExpandSpeckleObject()
      : base("Expand Speckle Object", "ESO",
          "Allows you to decompose a Speckle object in its constituent parts.",
          "Speckle 2", "Object Management")
    {
      Kit = KitManager.GetDefaultKit();
      try
      {
        Converter = Kit.LoadConverter(Applications.Rhino);
        Message = $"Using the \n{Kit.Name}\n Kit Converter";
      }
      catch
      {
        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "No default kit found on this machine.");
      }
    }

    public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
    {
      Menu_AppendSeparator(menu);
      Menu_AppendItem(menu, "Select the converter you want to use:");

      var kits = KitManager.GetKitsWithConvertersForApp(Applications.Rhino);

      foreach (var kit in kits)
      {
        Menu_AppendItem(menu, $"{kit.Name} ({kit.Description})", (s, e) => { SetConverterFromKit(kit.Name); }, true, kit.Name == Kit.Name);
      }

      Menu_AppendSeparator(menu);
    }

    private void SetConverterFromKit(string kitName)
    {
      if (kitName == Kit.Name) return;

      Kit = KitManager.Kits.FirstOrDefault(k => k.Name == kitName);
      Converter = Kit.LoadConverter(Applications.Rhino);

      Message = $"Using the {Kit.Name} Converter";
      ExpireSolution(true);
    }

    public override bool Read(GH_IReader reader)
    {
      // TODO: Read kit name and instantiate converter
      return base.Read(reader);
    }

    public override bool Write(GH_IWriter writer)
    {
      // TODO: Write kit name to disk
      return base.Write(writer);
    }

    protected override void RegisterInputParams(GH_InputParamManager pManager)
    {
      pManager.AddGenericParameter("Speckle Object", "O", "Speckle object to deconstruct into it's properties.", GH_ParamAccess.tree);
    }
    protected override void RegisterOutputParams(GH_OutputParamManager pManager)
    {
      // All output params are dynamically generated!
    }
    
    protected override void BeforeSolveInstance() {
      if (speckleObjects != null && hasSetData)
        AutoCreateOutputs();
      base.BeforeSolveInstance();
    }
    
    private bool hasSetData;
    private GH_Structure<IGH_Goo> speckleObjects;
    private List<string> outputList = new List<string>();
    
    private List<string> GetOutputList()
    {
      // Get the full list of output parameters
      var fullProps = new List<string>();
      foreach (var path in speckleObjects.Paths)
      {
        var obj = speckleObjects.get_DataItem(path, 0);
        var b = (obj as GH_SpeckleBase).Value;
        var props = b.GetDynamicMembers().ToList();
        props.ForEach(prop =>
        {
          if(!fullProps.Contains(prop)) fullProps.Add(prop);
        });
      }
      return fullProps;
    }
    
    protected override void SolveInstance(IGH_DataAccess DA)
    {
      if (!hasSetData)
      { 
        // First run: Save the tree and expire solution to force an update in the output params.
        
        // Get the data or abort.
        if(!DA.GetDataTree(0, out speckleObjects)) return;
        
        // Ensure only one object per path.
        speckleObjects.Graft(GH_GraftMode.GraftAll); 
        
        // Update the output list
        outputList = GetOutputList();
        
        // Once data has been set, expire solution to update output params.
        hasSetData = true;
        ExpireSolution(true);
      }
      else
      { 
        // Second run: Parameter output should have been updated in `beforeSolveInstance` with latest state.
        
        // Build the output dictionary
        var outputDict = CreateOutputDictionary();
        
        // Assign outputs.
        foreach (var key in outputDict.Keys)
          DA.SetDataTree(Params.IndexOfOutputParam(key), outputDict[key]);
        
        // Reset state
        hasSetData = false;
        speckleObjects = null;
      }
    }

    private Dictionary<string, GH_Structure<GH_ObjectWrapper>> CreateOutputDictionary()
    {
      // Create empty data tree placeholders for output.
      var outputDict = outputList.ToDictionary(outParam => outParam, _ => new GH_Structure<GH_ObjectWrapper>());
      
      // Assign all values to it's corresponding dictionary entry and branch path.
      foreach (var path in speckleObjects.Paths)
      {
        // Loop through all dynamic properties
        var obj = ((GH_SpeckleBase)speckleObjects.get_DataItem(path, 0)).Value;
        foreach (var prop in obj.GetDynamicMembers())
        {
          // Convert and add to corresponding output structure
          var value = obj[prop];
          if (value is List<object> list)
            outputDict[prop].AppendRange(list.Select(
                item => new GH_ObjectWrapper(TryConvertItem(item))),
              path);
          else
            outputDict[prop].Append(
              new GH_ObjectWrapper(TryConvertItem(obj[prop])),
              path);
        }
      }

      return outputDict;
    }

    private object TryConvertItem(object value)
    {
      if (value is IGH_Goo)
      {
        value = value.GetType().GetProperty("Value")?.GetValue(value);
      }
      if (value is Base @base && Converter.CanConvertToNative(@base))
      {
        return Converter.ConvertToNative(@base);
      }
      if (value.GetType().IsSimpleType())
      {
        return value;
      }
      return null;
    }

    public bool CanInsertParameter(GH_ParameterSide side, int index) => false;

    public bool CanRemoveParameter(GH_ParameterSide side, int index) => false;

    public IGH_Param CreateParameter(GH_ParameterSide side, int index)
    {
      var myParam = new GenericAccessParam
      {
          Name = GH_ComponentParamServer.InventUniqueNickname("ABCD", Params.Input),
          MutableNickName = true,
        Optional = true
      };

      myParam.NickName = myParam.Name;
      myParam.ObjectChanged += (sender, e) => { };

      return myParam;
    }

    public bool DestroyParameter(GH_ParameterSide side, int index)
    {
      return true;
    }

    public void VariableParameterMaintenance()
    { 
      if (outputList.Count == 0) return;

      for (var i = 0; i < Params.Output.Count; i++)
      {
        if (i > outputList.Count - 1) return;
        
        var name = outputList[i];
        Params.Output[i].Name = $"{name}";
        Params.Output[i].NickName = $"{name}";
        Params.Output[i].Description = $"Data from property: {name}";
        Params.Output[i].MutableNickName = false;
        Params.Output[i].Access = GH_ParamAccess.tree;
      }
    }

    private bool OutputMismatch()
    {
      bool countMatch = outputList.Count == Params.Output.Count;
      if (!countMatch) 
        return true;
        
      for (var i = 0; i < outputList.Count; i++)
        if (Params.Output[i].NickName != outputList[i])
          return true;

      return false;
    }

    private void AutoCreateOutputs()
    {
      int tokenCount = outputList.Count;
      
      if (tokenCount == 0 || !OutputMismatch()) return;
      RecordUndoEvent("Creating Outputs");
      
      if (Params.Output.Count < tokenCount)
        while (Params.Output.Count < tokenCount)
        {
          IGH_Param newParam = CreateParameter(GH_ParameterSide.Output, Params.Output.Count);
          Params.RegisterOutputParam(newParam);
        }
      else if (Params.Output.Count > tokenCount)
        while (Params.Output.Count > tokenCount)
        {
          Params.UnregisterOutputParameter(Params.Output[Params.Output.Count - 1]);
        }
      
      Params.OnParametersChanged();
      VariableParameterMaintenance();
    }

  }
}
