// NClass - Free class diagram editor
// Copyright (C) 2006-2007 Balazs Tihanyi
// 
// This program is free software; you can redistribute it and/or modify it under 
// the terms of the GNU General Public License as published by the Free Software 
// Foundation; either version 3 of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful, but WITHOUT 
// ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS 
// FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License along with 
// this program; if not, write to the Free Software Foundation, Inc., 
// 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using NClass.Core;
using NClass.DiagramEditor.ClassDiagram.Shapes;
using NClass.DiagramEditor.Properties;
using NClass.Translations;

namespace NClass.DiagramEditor.ClassDiagram.ContextMenus
{
    internal sealed class TypeShapeContextMenu : DiagramContextMenu
    {
        private ToolStripMenuItem mnuAutoHeight;
        private ToolStripMenuItem mnuAutoSize;
        private ToolStripMenuItem mnuAutoWidth;
        private ToolStripMenuItem mnuCollapseAllSelected;
        private ToolStripMenuItem mnuEditMembers;
        private ToolStripMenuItem mnuAddToNamespace;
        private ToolStripMenuItem mnuExpandAllSelected;

        private ToolStripMenuItem mnuSize;

        private List<ToolStripItem> generatedNamespaceItems = new List< ToolStripItem >();

        private TypeShapeContextMenu( )
        {
            InitMenuItems( );
        }

        public static TypeShapeContextMenu Default { get; } = new TypeShapeContextMenu( );

        private void UpdateTexts( )
        {
            mnuSize.Text = Strings.MenuSize;
            mnuAutoSize.Text = Strings.MenuAutoSize;
            mnuAutoWidth.Text = Strings.MenuAutoWidth;
            mnuAutoHeight.Text = Strings.MenuAutoHeight;
            mnuCollapseAllSelected.Text = Strings.MenuCollapseAllSelected;
            mnuExpandAllSelected.Text = Strings.MenuExpandAllSelected;
            mnuEditMembers.Text = Strings.MenuEditMembers;
        }

        public override void ValidateMenuItems( Diagram diagram )
        {
            base.ValidateMenuItems( diagram );
            ShapeContextMenu.Default.ValidateMenuItems( diagram );
            mnuEditMembers.Enabled = diagram.SelectedElementCount == 1;

            generatedNamespaceItems.Clear(  );
            if (MenuList.Contains(mnuAddToNamespace))
                MenuList.Remove(mnuAddToNamespace);


            generatedNamespaceItems.Add( new ToolStripMenuItem(Strings.None, null, ( sender, args ) =>
            {
                TypeShape typeShape = Diagram.TopSelectedElement as TypeShape;
                if (typeShape != null)
                {
                    typeShape.TypeBase.ParentNameSpace = null;
                }
            }) );
            foreach ( IEntity diagramEntity in Diagram.Entities )
            {
                if ( diagramEntity is Namespace )
                {
                    string name = ( diagramEntity as Namespace ).Name;
                    ToolStripItem item = new ToolStripMenuItem(name, null, ( sender, args ) =>
                    {
                        TypeShape typeShape = Diagram.TopSelectedElement as TypeShape;
                        if (typeShape != null)
                        {
                            typeShape.TypeBase.ParentNameSpace = ( diagramEntity as Namespace );
                        }
                    } );
                    generatedNamespaceItems.Add( item );
                }
            }

            mnuAddToNamespace = new ToolStripMenuItem( Strings.MenuAddToNamespace, null, generatedNamespaceItems.ToArray( ) );
            MenuList.Add( mnuAddToNamespace );
        }

        private void InitMenuItems( )
        {
            mnuEditMembers = new ToolStripMenuItem( Strings.MenuEditMembers, Resources.EditMembers, mnuEditMembers_Click );
            mnuAddToNamespace = new ToolStripMenuItem( Strings.MenuAddToNamespace, null);
            mnuAutoSize = new ToolStripMenuItem( Strings.MenuAutoSize, null, mnuAutoSize_Click );
            mnuAutoWidth = new ToolStripMenuItem( Strings.MenuAutoWidth, null, mnuAutoWidth_Click );
            mnuAutoHeight = new ToolStripMenuItem( Strings.MenuAutoHeight, null, mnuAutoHeight_Click );
            mnuCollapseAllSelected = new ToolStripMenuItem( Strings.MenuCollapseAllSelected, null, mnuCollapseAllSelected_Click );
            mnuExpandAllSelected = new ToolStripMenuItem( Strings.MenuExpandAllSelected, null, mnuExpandAllSelected_Click );
            mnuSize = new ToolStripMenuItem( Strings.MenuSize, null, mnuAutoSize, mnuAutoWidth, mnuAutoHeight, new ToolStripSeparator( ), mnuCollapseAllSelected, mnuExpandAllSelected );

            MenuList.AddRange( ShapeContextMenu.Default.MenuItems );
            MenuList.AddRange( new ToolStripItem[] {mnuSize, new ToolStripSeparator( ), mnuEditMembers } );
        }

        private void mnuAutoSize_Click( object sender, EventArgs e )
        {
            if ( Diagram != null )
                Diagram.AutoSizeOfSelectedShapes( );
        }

        private void mnuAutoWidth_Click( object sender, EventArgs e )
        {
            if ( Diagram != null )
                Diagram.AutoWidthOfSelectedShapes( );
        }

        private void mnuAutoHeight_Click( object sender, EventArgs e )
        {
            if ( Diagram != null )
                Diagram.AutoHeightOfSelectedShapes( );
        }

        private void mnuCollapseAllSelected_Click( object sender, EventArgs e )
        {
            if ( Diagram != null )
                Diagram.CollapseAll( true );
        }

        private void mnuExpandAllSelected_Click( object sender, EventArgs e )
        {
            if ( Diagram != null )
                Diagram.ExpandAll( true );
        }

        private void mnuEditMembers_Click( object sender, EventArgs e )
        {
            if ( Diagram != null )
            {
                TypeShape typeShape = Diagram.TopSelectedElement as TypeShape;
                if ( typeShape != null )
                {
                    typeShape.IsActive = false;
                    typeShape.EditMembers( );
                }
            }
        }
    }
}