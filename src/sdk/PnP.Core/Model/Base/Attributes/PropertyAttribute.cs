﻿using System;

namespace PnP.Core.Model
{
    /// <summary>
    /// Base class for attributes that define how a model property maps to the field used in either SharePoint REST or Microsoft Graph
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    internal abstract class PropertyAttribute : Attribute
    {
        /// <summary>
        /// Name of the SharePoint field
        /// </summary>
        public string FieldName { get; set; }

        /// <summary>
        /// Expand this property when there's a default get (a get without specifying the fields to load)
        /// </summary>
        public bool ExpandByDefault { get; set; }

        /// <summary>
        /// Json path that contains the value we need, relative to the object loaded via FieldName
        /// This works for simple JSON objects without arrays, delimiter will be a dot e.g. property.subproperty.subsub
        /// </summary>
        public string JsonPath { get; set; }

        /// <summary>
        /// Force a call out to the custom mapping handler for this field / the default mapping logic will not be applied
        /// </summary>
        public bool UseCustomMapping { get; set; }

        /// <summary>
        /// Url to get this field, needed in case the field cannot be loaded via an expand
        /// </summary>
        public string Get { get; set; }
    }
}
