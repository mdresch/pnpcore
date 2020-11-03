﻿using System.Collections.Generic;

namespace PnP.Core.Model.SharePoint
{
    /// <summary>
    /// Represents a section on the canvas
    /// </summary>
    public interface ICanvasSection
    {
        /// <summary>
        /// Type of the section
        /// </summary>
        CanvasSectionTemplate Type { get; set; }

        /// <summary>
        /// Order in which this section is presented on the page
        /// </summary>
        float Order { get; set; }

        /// <summary>
        /// <see cref="CanvasColumn"/> instances that are part of this section
        /// </summary>
        List<ICanvasColumn> Columns { get; }

        /// <summary>
        /// The <see cref="IPage"/> instance holding this section
        /// </summary>
        IPage Page { get; }

        /// <summary>
        /// Controls hosted in this section
        /// </summary>
        List<ICanvasControl> Controls { get; }

        /// <summary>
        /// The default <see cref="CanvasColumn"/> of this section
        /// </summary>
        ICanvasColumn DefaultColumn { get; }

        /// <summary>
        /// A page can contain one section that has a vertical section column...use this attribute to get that column
        /// </summary>
        ICanvasColumn VerticalSectionColumn { get; }

        /// <summary>
        /// Color emphasis of the section 
        /// </summary>
        int ZoneEmphasis { get; }        
    }
}
