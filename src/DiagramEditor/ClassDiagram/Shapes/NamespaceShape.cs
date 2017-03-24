// NClass - Free class diagram editor
// Copyright (C) 2006-2009 Balazs Tihanyi
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
using System.Drawing;
using System.Windows.Forms;
using NClass.Core;
using NClass.DiagramEditor.ClassDiagram.Dialogs;
using NClass.DiagramEditor.ClassDiagram.Editors;
using NClass.DiagramEditor.Properties;

namespace NClass.DiagramEditor.ClassDiagram.Shapes
{
    internal sealed class NamespaceShape : Shape
    {
        private bool editorShowed = false;
       
        NamespaceEditor editor = new NamespaceEditor(  );

        internal NamespaceShape(Namespace nspace) : base(nspace)
        {
            Namespace = nspace;
        }

        public Namespace Namespace { get; }

        public override void Draw( IGraphics g, bool onScreen, Style style )
        {
            g.DrawString( this.Namespace.Name, style.NameFont, Brushes.CadetBlue, this.Location );
            if(IsSelected)g.DrawString( "Drag here", style.NameFont, Brushes.CadetBlue, new PointF(this.Right - 80, this.Top));
            g.DrawLine( new Pen( Color.CadetBlue, 1 ), this.Location.X, this.Location.Y + 15, this.Location.X + this.Size.Width, this.Location.Y + 15 );
            g.DrawRectangle( new Pen( Color.CadetBlue, 1 ), new Rectangle(Location, Size) );
        }

        internal override void MousePressed( AbsoluteMouseEventArgs e )
        {
            if ( InEdges( e.Location, 10, 20 ) )
                base.MousePressed( e );
            else
                HideEditor(  );
        }

        private bool InEdges( PointF point, float margin, float topMargin )
        {
            return 
                ( ( Math.Abs( point.X - this.Left ) < margin || Math.Abs( point.X - this.Right ) < margin ) ) ||
                ( ( Math.Abs( point.Y - this.Top) < topMargin|| Math.Abs( point.Y - this.Bottom) < margin ) );
        }

        protected override void OnMove(MoveEventArgs e)
        {
            base.OnMove(e);
            HideEditor();
        }

        protected override void OnResize(ResizeEventArgs e)
        {
            base.OnResize(e);
            if (editorShowed)
            {
                editor.Relocate(this);
                if (!editor.Focused)
                    editor.Focus();
            }
        }

        protected override void OnDoubleClick(AbsoluteMouseEventArgs e)
        {
            if (InEdges(e.Location, 10,20) && (e.Button == MouseButtons.Left))
                ShowEditor();
        }

        protected internal override void ShowEditor()
        {
            if (!editorShowed)
            {
                editor.Relocate(this);
                editor.Init(this);
                ShowWindow(editor);
                editor.Focus();
                editorShowed = true;
            }
        }

        protected internal override void HideEditor()
        {
            if (editorShowed)
            {
                HideWindow(editor);
                editorShowed = false;
            }
        }

        protected internal override void MoveWindow()
        {
            HideEditor();
        }

        protected override Size DefaultSize
        {
            get
            {
                return new Size(200,100);
            }
        }

        public override IEntity Entity
        {
            get { return Namespace; }
        }

        protected override int GetBorderWidth( Style style )
        {
            return style.EnumBorderWidth;
        }

        protected override bool CloneEntity( Diagram diagram )
        {
            return diagram.InsertNamespace(Namespace.Clone());
        }
    }
}