using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConverterRevitShared.Revit;

public class FamilyLoadOption : IFamilyLoadOptions
{
  public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
  {
    overwriteParameterValues = true;
    return true;
  }

  public bool OnSharedFamilyFound(
    Family sharedFamily,
    bool familyInUse,
    out FamilySource source,
    out bool overwriteParameterValues
  )
  {
    source = FamilySource.Family;
    overwriteParameterValues = true;
    return true;
  }
}
