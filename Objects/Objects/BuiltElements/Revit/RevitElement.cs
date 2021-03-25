﻿using Objects.Geometry;
using Speckle.Core.Kits;
using Speckle.Core.Models;
using System.Collections.Generic;

namespace Objects.BuiltElements.Revit
{
  /// <summary>
  /// A generic Revit element for which we don't have direct conversions
  /// </summary>
  public class RevitElement : Base, IDisplayMesh
  {
    public string family { get; set; }
    public string type { get; set; }
    public string category { get; set; }
    public List<Parameter> parameters { get; set; }
    public string elementId { get; set; }

    [DetachProperty]
    public Mesh displayMesh { get; set; }

    public RevitElement() { }
  }

}
