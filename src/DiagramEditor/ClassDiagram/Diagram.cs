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
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Xml;
using NClass.Core;
using NClass.DiagramEditor.ClassDiagram.Connections;
using NClass.DiagramEditor.ClassDiagram.ContextMenus;
using NClass.DiagramEditor.ClassDiagram.Dialogs;
using NClass.DiagramEditor.ClassDiagram.Shapes;
using NClass.Translations;

namespace NClass.DiagramEditor.ClassDiagram
{
    public class Diagram : Model, IDocument, IEditable, IPrintable
    {
        private const int DiagramPadding = 10;
        private const int PrecisionSize = 10;
        private const int MaximalPrecisionDistance = 500;
        private const float DashSize = 3;
        private static readonly Size MinSize = new Size( 3000, 2000 );
        public static readonly Pen SelectionPen;
        private DiagramElement activeElement;
        private ConnectionCreator connectionCreator;
        private PointF mouseLocation = PointF.Empty;
        private EntityType newShapeType = EntityType.Class;
        private Point offset = Point.Empty;
        private bool redrawSuspended;
        private RectangleF selectionFrame = RectangleF.Empty;
        private bool selectioning;
        private Rectangle shapeOutline = Rectangle.Empty;

        private EntityType shapeType;
        private Size size = MinSize;

        private State state = State.Normal;
        private float zoom = 1.0F;

        static Diagram( )
        {
            SelectionPen = new Pen( Color.Black );
            SelectionPen.DashPattern = new[] {DashSize, DashSize};
        }

        protected Diagram( ) {}

        /// <exception cref="ArgumentNullException">
        ///     <paramref name="language" /> is null.
        /// </exception>
        public Diagram( Language language ) : base( language ) {}

        /// <exception cref="ArgumentException">
        ///     <paramref name="name" /> cannot be empty string.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="language" /> is null.
        /// </exception>
        public Diagram( string name, Language language ) : base( name, language ) {}

        public IEnumerable< Shape > Shapes
        {
            get { return ShapeList; }
        }

        protected internal ElementList< Shape > ShapeList { get; } = new ElementList< Shape >( );

        public IEnumerable< Connection > Connections
        {
            get { return ConnectionList; }
        }

        protected internal ElementList< Connection > ConnectionList { get; } = new ElementList< Connection >( );

        public bool RedrawSuspended
        {
            get { return redrawSuspended; }
            set
            {
                if ( redrawSuspended != value )
                {
                    redrawSuspended = value;
                    if ( !redrawSuspended )
                    {
                        RecalculateSize( );
                        RequestRedrawIfNeeded( );
                    }
                }
            }
        }

        public int ShapeCount
        {
            get { return ShapeList.Count; }
        }

        public int ConnectionCount
        {
            get { return ConnectionList.Count; }
        }

        public DiagramElement ActiveElement
        {
            get { return activeElement; }
            private set
            {
                if ( activeElement != null )
                    activeElement.IsActive = false;
                activeElement = value;
            }
        }

        public DiagramElement TopSelectedElement
        {
            get
            {
                if ( SelectedConnectionCount > 0 )
                    return ConnectionList.FirstValue;
                if ( SelectedShapeCount > 0 )
                    return ShapeList.FirstValue;
                return null;
            }
        }

        public int SelectedElementCount
        {
            get { return SelectedShapeCount + SelectedConnectionCount; }
        }

        public int SelectedShapeCount { get; private set; }

        public int SelectedConnectionCount { get; private set; }

        public event EventHandler OffsetChanged;
        public event EventHandler SizeChanged;
        public event EventHandler ZoomChanged;
        public event EventHandler StatusChanged;
        public event EventHandler NeedsRedraw;
        public event EventHandler ClipboardAvailabilityChanged;
        public event PopupWindowEventHandler ShowingWindow;
        public event PopupWindowEventHandler HidingWindow;

        public Point Offset
        {
            get { return offset; }
            set
            {
                if ( value.X < 0 )
                    value.X = 0;
                if ( value.Y < 0 )
                    value.Y = 0;

                if ( offset != value )
                {
                    offset = value;
                    OnOffsetChanged( EventArgs.Empty );
                }
            }
        }

        public Size Size
        {
            get { return size; }
            protected set
            {
                if ( value.Width < MinSize.Width )
                    value.Width = MinSize.Width;
                if ( value.Height < MinSize.Height )
                    value.Height = MinSize.Height;

                if ( size != value )
                {
                    size = value;
                    OnSizeChanged( EventArgs.Empty );
                }
            }
        }

        public float Zoom
        {
            get { return zoom; }
            set
            {
                if ( value < Canvas.MinZoom )
                    value = Canvas.MinZoom;
                if ( value > Canvas.MaxZoom )
                    value = Canvas.MaxZoom;

                if ( zoom != value )
                {
                    zoom = value;
                    OnZoomChanged( EventArgs.Empty );
                }
            }
        }

        public Color BackColor
        {
            get { return Style.CurrentStyle.BackgroundColor; }
        }

        public bool CanCutToClipboard
        {
            get { return SelectedShapeCount > 0; }
        }

        public bool CanCopyToClipboard
        {
            get { return SelectedShapeCount > 0; }
        }

        public bool CanPasteFromClipboard
        {
            get { return Clipboard.Item is ElementContainer; }
        }

        public bool HasSelectedElement
        {
            get { return SelectedElementCount > 0; }
        }

        public string GetSelectedElementName( )
        {
            if ( HasSelectedElement && ( SelectedElementCount == 1 ) )
                foreach ( Shape shape in ShapeList )
                    if ( shape.IsSelected )
                        return shape.Entity.Name;

            return null;
        }

        public void CloseWindows( )
        {
            if ( ActiveElement != null )
                ActiveElement.HideEditor( );
        }

        public void Cut( )
        {
            if ( CanCutToClipboard )
            {
                Copy( );
                DeleteSelectedElements( false );
            }
        }

        public void Copy( )
        {
            if ( CanCopyToClipboard )
            {
                ElementContainer elements = new ElementContainer( );
                foreach ( Shape shape in GetSelectedShapes( ) )
                    elements.AddShape( shape );
                foreach ( Connection connection in GetSelectedConnections( ) )
                    elements.AddConnection( connection );
                Clipboard.Item = elements;
            }
        }

        public void Paste( )
        {
            if ( CanPasteFromClipboard )
            {
                DeselectAll( );
                RedrawSuspended = true;
                Clipboard.Paste( this );
                RedrawSuspended = false;
                OnClipboardAvailabilityChanged( EventArgs.Empty );
            }
        }

        public void Display( Graphics g )
        {
            RectangleF clip = g.ClipBounds;

            // Draw diagram elements
            IGraphics graphics = new GdiGraphics( g );
            foreach ( DiagramElement element in GetElementsInReversedDisplayOrder( ) )
            {
                if ( clip.IntersectsWith( element.GetVisibleArea( Zoom ) ) )
                    element.Draw( graphics, true );
                element.NeedsRedraw = false;
            }
            if ( state == State.CreatingShape )
                g.DrawRectangle( SelectionPen, shapeOutline.X, shapeOutline.Y, shapeOutline.Width, shapeOutline.Height );
            else if ( state == State.CreatingConnection )
                connectionCreator.Draw( g );

            // Draw selection lines
            GraphicsState savedState = g.Save( );
            g.ResetTransform( );
            g.SmoothingMode = SmoothingMode.None;
            foreach ( Shape shape in ShapeList.GetSelectedElementsReversed( ) )
                if ( clip.IntersectsWith( shape.GetVisibleArea( Zoom ) ) )
                    shape.DrawSelectionLines( g, Zoom, Offset );
            foreach ( Connection connection in ConnectionList.GetSelectedElementsReversed( ) )
                if ( clip.IntersectsWith( connection.GetVisibleArea( Zoom ) ) )
                    connection.DrawSelectionLines( g, Zoom, Offset );

            if ( state == State.Multiselecting )
            {
                RectangleF frame = RectangleF.FromLTRB( Math.Min( selectionFrame.Left, selectionFrame.Right ), Math.Min( selectionFrame.Top, selectionFrame.Bottom ), Math.Max( selectionFrame.Left, selectionFrame.Right ), Math.Max( selectionFrame.Top, selectionFrame.Bottom ) );
                g.DrawRectangle( SelectionPen, frame.X * Zoom - Offset.X, frame.Y * Zoom - Offset.Y, frame.Width * Zoom, frame.Height * Zoom );
            }

            // Draw diagram border
            clip = g.ClipBounds;
            float borderWidth = Size.Width * Zoom;
            float borderHeight = Size.Height * Zoom;
            if ( ( clip.Right > borderWidth ) || ( clip.Bottom > borderHeight ) )
            {
                SelectionPen.DashOffset = Offset.Y - Offset.X;
                g.DrawLines( SelectionPen, new[] {new PointF( borderWidth, 0 ), new PointF( borderWidth, borderHeight ), new PointF( 0, borderHeight )} );
                SelectionPen.DashOffset = 0;
            }

            // Restore original state
            g.Restore( savedState );
        }

        public void ShowPrintDialog( )
        {
            DiagramPrintDialog dialog = new DiagramPrintDialog( );
            dialog.Document = this;
            dialog.ShowDialog( );
        }

        public void Print( IGraphics g, bool selectedOnly, Style style )
        {
            foreach ( Shape shape in ShapeList.GetReversedList( ) )
                if ( !selectedOnly || shape.IsSelected )
                    shape.Draw( g, false, style );
            foreach ( Connection connection in ConnectionList.GetReversedList( ) )
                if ( !selectedOnly || connection.IsSelected )
                    connection.Draw( g, false, style );
        }

        public void SelectAll( )
        {
            RedrawSuspended = true;
            selectioning = true;

            foreach ( Shape shape in ShapeList )
                shape.IsSelected = true;
            foreach ( Connection connection in ConnectionList )
                connection.IsSelected = true;

            SelectedShapeCount = ShapeList.Count;
            SelectedConnectionCount = ConnectionList.Count;

            OnSelectionChanged( EventArgs.Empty );
            OnClipboardAvailabilityChanged( EventArgs.Empty );
            OnSatusChanged( EventArgs.Empty );

            selectioning = false;
            RedrawSuspended = false;
        }

        public void DeleteSelectedElements( )
        {
            DeleteSelectedElements( true );
        }

        public void Redraw( )
        {
            OnNeedsRedraw( EventArgs.Empty );
        }

        public DynamicMenu GetDynamicMenu( )
        {
            DynamicMenu dynamicMenu = DiagramDynamicMenu.Default;
            dynamicMenu.SetReference( this );
            return dynamicMenu;
        }

        public ContextMenuStrip GetContextMenu( AbsoluteMouseEventArgs e )
        {
            if ( HasSelectedElement )
            {
                Intersector< ToolStripItem > intersector = new Intersector< ToolStripItem >( );
                ContextMenu.MenuStrip.Items.Clear( );

                foreach ( Shape shape in GetSelectedShapes( ) )
                    intersector.AddSet( shape.GetContextMenuItems( this ) );
                foreach ( Connection connection in GetSelectedConnections( ) )
                    intersector.AddSet( connection.GetContextMenuItems( this ) );

                foreach ( ToolStripItem menuItem in intersector.GetIntersection( ) )
                    ContextMenu.MenuStrip.Items.Add( menuItem );
                return ContextMenu.MenuStrip;
            }
            ContextMenu.MenuStrip.Items.Clear( );
            foreach ( ToolStripItem menuItem in BlankContextMenu.Default.GetMenuItems( this ) )
                ContextMenu.MenuStrip.Items.Add( menuItem );

            return ContextMenu.MenuStrip;
        }

        public string GetStatus( )
        {
            if ( SelectedElementCount == 1 )
                return TopSelectedElement.ToString( );
            if ( SelectedElementCount > 1 )
                return string.Format( Strings.ItemsSelected, SelectedElementCount );
            return Strings.Ready;
        }

        public string GetShortDescription( )
        {
            return Strings.Language + ": " + Language;
        }

        public void MouseDown( AbsoluteMouseEventArgs e )
        {
            RedrawSuspended = true;
            if ( state == State.CreatingShape )
            {
                AddCreatedShape( );
            }
            else if ( state == State.CreatingConnection )
            {
                connectionCreator.MouseDown( e );
                if ( connectionCreator.Created )
                    state = State.Normal;
            }
            else
            {
                SelectElements( e );
            }

            if ( e.Button == MouseButtons.Right )
                ActiveElement = null;

            RedrawSuspended = false;
        }

        public void MouseMove( AbsoluteMouseEventArgs e )
        {
            RedrawSuspended = true;

            mouseLocation = e.Location;
            if ( state == State.Multiselecting )
            {
                selectionFrame = RectangleF.FromLTRB( selectionFrame.Left, selectionFrame.Top, e.X, e.Y );
                Redraw( );
            }
            else if ( state == State.CreatingShape )
            {
                shapeOutline.Location = new Point( ( int ) e.X, ( int ) e.Y );
                Redraw( );
            }
            else if ( state == State.CreatingConnection )
            {
                connectionCreator.MouseMove( e );
            }
            else
            {
                foreach ( DiagramElement element in GetElementsInDisplayOrder( ) )
                    element.MouseMoved( e );
            }

            RedrawSuspended = false;
        }

        public void MouseUp( AbsoluteMouseEventArgs e )
        {
            RedrawSuspended = true;

            if ( state == State.Multiselecting )
            {
                TrySelectElements( );
                state = State.Normal;
            }
            else
            {
                foreach ( DiagramElement element in GetElementsInDisplayOrder( ) )
                    element.MouseUpped( e );
            }

            RedrawSuspended = false;
        }

        public void DoubleClick( AbsoluteMouseEventArgs e )
        {
            foreach ( DiagramElement element in GetElementsInDisplayOrder( ) )
                element.DoubleClicked( e );
        }

        public void KeyDown( KeyEventArgs e )
        {
            //TODO: ActiveElement.KeyDown() - de nem minden esetben (pl. t�rl�sn�l nem)
            RedrawSuspended = true;

            // Delete
            if ( e.KeyCode == Keys.Delete )
            {
                if ( ( SelectedElementCount >= 2 ) || ( ActiveElement == null ) || !ActiveElement.DeleteSelectedMember( ) )
                    DeleteSelectedElements( );
            }
            // Escape
            else if ( e.KeyCode == Keys.Escape )
            {
                state = State.Normal;
                DeselectAll( );
                Redraw( );
            }
            // Enter
            else if ( ( e.KeyCode == Keys.Enter ) && ( ActiveElement != null ) )
            {
                ActiveElement.ShowEditor( );
            }
            // Up
            else if ( ( e.KeyCode == Keys.Up ) && ( ActiveElement != null ) )
            {
                if ( e.Shift || e.Control )
                    ActiveElement.MoveUp( );
                else
                    ActiveElement.SelectPrevious( );
            }
            // Down
            else if ( ( e.KeyCode == Keys.Down ) && ( ActiveElement != null ) )
            {
                if ( e.Shift || e.Control )
                    ActiveElement.MoveDown( );
                else
                    ActiveElement.SelectNext( );
            }
            // Ctrl + X
            else if ( ( e.KeyCode == Keys.X ) && ( e.Modifiers == Keys.Control ) )
            {
                Cut( );
            }
            // Ctrl + C
            else if ( ( e.KeyCode == Keys.C ) && ( e.Modifiers == Keys.Control ) )
            {
                Copy( );
            }
            // Ctrl + V
            else if ( ( e.KeyCode == Keys.V ) && ( e.Modifiers == Keys.Control ) )
            {
                Paste( );
            }
            // Ctrl + Shift + ?
            else if ( e.Modifiers == ( Keys.Control | Keys.Shift ) )
            {
                switch ( e.KeyCode )
                {
                    case Keys.A:
                        CreateShape( );
                        break;

                    case Keys.C:
                        CreateShape( EntityType.Class );
                        break;

                    case Keys.S:
                        CreateShape( EntityType.Structure );
                        break;

                    case Keys.I:
                        CreateShape( EntityType.Interface );
                        break;

                    case Keys.E:
                        CreateShape( EntityType.Enum );
                        break;

                    case Keys.D:
                        CreateShape( EntityType.Delegate );
                        break;

                    case Keys.N:
                        CreateShape( EntityType.Comment );
                        break;
                }
            }
            RedrawSuspended = false;
        }

        public RectangleF GetPrintingArea( bool selectedOnly )
        {
            RectangleF area = Rectangle.Empty;
            bool first = true;

            foreach ( Shape shape in ShapeList )
                if ( !selectedOnly || shape.IsSelected )
                    if ( first )
                    {
                        area = shape.GetPrintingClip( Zoom );
                        first = false;
                    }
                    else
                    {
                        area = RectangleF.Union( area, shape.GetPrintingClip( Zoom ) );
                    }
            foreach ( Connection connection in ConnectionList )
                if ( !selectedOnly || connection.IsSelected )
                    if ( first )
                    {
                        area = connection.GetPrintingClip( Zoom );
                        first = false;
                    }
                    else
                    {
                        area = RectangleF.Union( area, connection.GetPrintingClip( Zoom ) );
                    }

            return area;
        }

        public event EventHandler SelectionChanged;

        public IEnumerable< Shape > GetSelectedShapes( )
        {
            return ShapeList.GetSelectedElements( );
        }

        public IEnumerable< Connection > GetSelectedConnections( )
        {
            return ConnectionList.GetSelectedElements( );
        }

        public IEnumerable< DiagramElement > GetSelectedElements( )
        {
            foreach ( Shape shape in ShapeList )
                if ( shape.IsSelected )
                    yield return shape;
            foreach ( Connection connection in ConnectionList )
                if ( connection.IsSelected )
                    yield return connection;
        }

        private IEnumerable< DiagramElement > GetElementsInDisplayOrder( )
        {
            foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                yield return shape;

            foreach ( Connection connection in ConnectionList.GetSelectedElements( ) )
                yield return connection;

            foreach ( Connection connection in ConnectionList.GetUnselectedElements( ) )
                yield return connection;

            foreach ( Shape shape in ShapeList.GetUnselectedElements( ) )
                yield return shape;
        }

        private IEnumerable< DiagramElement > GetElementsInReversedDisplayOrder( )
        {
            foreach ( Shape shape in ShapeList.GetUnselectedElementsReversed( ) )
                yield return shape;

            foreach ( Connection connection in ConnectionList.GetUnselectedElementsReversed( ) )
                yield return connection;

            foreach ( Connection connection in ConnectionList.GetSelectedElementsReversed( ) )
                yield return connection;

            foreach ( Shape shape in ShapeList.GetSelectedElementsReversed( ) )
                yield return shape;
        }

        public void CopyAsImage( )
        {
            ImageCreator.CopyAsImage( this );
        }

        public void CopyAsImage( bool selectedOnly )
        {
            ImageCreator.CopyAsImage( this, selectedOnly );
        }

        public void SaveAsImage( )
        {
            ImageCreator.SaveAsImage( this );
        }

        public void SaveAsImage( bool selectedOnly )
        {
            ImageCreator.SaveAsImage( this, selectedOnly );
        }

        public void Print( IGraphics g )
        {
            Print( g, false, Style.CurrentStyle );
        }

        private void RecalculateSize( )
        {
            const int Padding = 500;
            int rightMax = MinSize.Width, bottomMax = MinSize.Height;

            foreach ( Shape shape in ShapeList )
            {
                Rectangle area = shape.GetLogicalArea( );
                if ( area.Right + Padding > rightMax )
                    rightMax = area.Right + Padding;
                if ( area.Bottom + Padding > bottomMax )
                    bottomMax = area.Bottom + Padding;
            }
            foreach ( Connection connection in ConnectionList )
            {
                Rectangle area = connection.GetLogicalArea( );
                if ( area.Right + Padding > rightMax )
                    rightMax = area.Right + Padding;
                if ( area.Bottom + Padding > bottomMax )
                    bottomMax = area.Bottom + Padding;
            }

            Size = new Size( rightMax, bottomMax );
        }

        public void AlignLeft( )
        {
            if ( SelectedShapeCount >= 2 )
            {
                int left = Size.Width;
                RedrawSuspended = true;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    left = Math.Min( left, shape.Left );
                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Left = left;

                RedrawSuspended = false;
            }
        }

        public void AlignRight( )
        {
            if ( SelectedShapeCount >= 2 )
            {
                int right = 0;
                RedrawSuspended = true;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    right = Math.Max( right, shape.Right );
                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Right = right;

                RedrawSuspended = false;
            }
        }

        public void AlignTop( )
        {
            if ( SelectedShapeCount >= 2 )
            {
                int top = Size.Height;
                RedrawSuspended = true;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    top = Math.Min( top, shape.Top );
                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Top = top;

                RedrawSuspended = false;
            }
        }

        public void AlignBottom( )
        {
            if ( SelectedShapeCount >= 2 )
            {
                int bottom = 0;
                RedrawSuspended = true;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    bottom = Math.Max( bottom, shape.Bottom );
                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Bottom = bottom;

                RedrawSuspended = false;
            }
        }

        public void AlignHorizontal( )
        {
            if ( SelectedShapeCount >= 2 )
            {
                int center = 0;
                RedrawSuspended = true;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    center += ( shape.Top + shape.Bottom ) / 2;
                center /= SelectedShapeCount;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Top = center - shape.Height / 2;

                RedrawSuspended = false;
            }
        }

        public void AlignVertical( )
        {
            if ( SelectedShapeCount >= 2 )
            {
                int center = 0;
                RedrawSuspended = true;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    center += ( shape.Left + shape.Right ) / 2;
                center /= SelectedShapeCount;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Left = center - shape.Width / 2;

                RedrawSuspended = false;
            }
        }

        public void AdjustToSameWidth( )
        {
            if ( SelectedShapeCount >= 2 )
            {
                int maxWidth = 0;
                RedrawSuspended = true;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    maxWidth = Math.Max( maxWidth, shape.Width );
                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Width = maxWidth;
                RedrawSuspended = false;
            }
        }

        public void AdjustToSameHeight( )
        {
            if ( SelectedShapeCount >= 2 )
            {
                int maxHeight = 0;
                RedrawSuspended = true;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    maxHeight = Math.Max( maxHeight, shape.Height );
                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Height = maxHeight;

                RedrawSuspended = false;
            }
        }

        public void AdjustToSameSize( )
        {
            if ( SelectedShapeCount >= 2 )
            {
                Size maxSize = Size.Empty;
                RedrawSuspended = true;

                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                {
                    maxSize.Width = Math.Max( maxSize.Width, shape.Width );
                    maxSize.Height = Math.Max( maxSize.Height, shape.Height );
                }
                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Size = maxSize;

                RedrawSuspended = false;
            }
        }

        public void AutoSizeOfSelectedShapes( )
        {
            RedrawSuspended = true;
            foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
            {
                shape.AutoWidth( );
                shape.AutoHeight( );
            }
            RedrawSuspended = false;
        }

        public void AutoWidthOfSelectedShapes( )
        {
            RedrawSuspended = true;
            foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                shape.AutoWidth( );
            RedrawSuspended = false;
        }

        public void AutoHeightOfSelectedShapes( )
        {
            RedrawSuspended = true;
            foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                shape.AutoHeight( );
            RedrawSuspended = false;
        }

        public void CollapseAll( )
        {
            bool selectedOnly = HasSelectedElement;
            CollapseAll( selectedOnly );
        }

        public void CollapseAll( bool selectedOnly )
        {
            RedrawSuspended = true;

            foreach ( Shape shape in ShapeList )
                if ( shape.IsSelected || !selectedOnly )
                    shape.Collapse( );

            RedrawSuspended = false;
        }

        public void ExpandAll( )
        {
            bool selectedOnly = HasSelectedElement;
            ExpandAll( selectedOnly );
        }

        public void ExpandAll( bool selectedOnly )
        {
            RedrawSuspended = true;

            foreach ( Shape shape in ShapeList )
                if ( shape.IsSelected || !selectedOnly )
                    shape.Expand( );

            RedrawSuspended = false;
        }

        private bool ConfirmDelete( )
        {
            DialogResult result = MessageBox.Show( Strings.DeleteElementsConfirmation, Strings.Confirmation, MessageBoxButtons.YesNo, MessageBoxIcon.Warning );

            return result == DialogResult.Yes;
        }

        private void DeleteSelectedElements( bool showConfirmation )
        {
            if ( HasSelectedElement && ( !showConfirmation || ConfirmDelete( ) ) )
            {
                if ( SelectedShapeCount > 0 )
                    foreach ( Shape shape in ShapeList.GetModifiableList( ) )
                        if ( shape.IsSelected )
                            RemoveEntity( shape.Entity );
                if ( SelectedConnectionCount > 0 )
                    foreach ( Connection connection in ConnectionList.GetModifiableList( ) )
                        if ( connection.IsSelected )
                            RemoveRelationship( connection.Relationship );
                Redraw( );
            }
        }

        private void RequestRedrawIfNeeded( )
        {
            if ( Loading )
                return;

            foreach ( Shape shape in ShapeList )
                if ( shape.NeedsRedraw )
                {
                    OnNeedsRedraw( EventArgs.Empty );
                    return;
                }
            foreach ( Connection connection in ConnectionList )
                if ( connection.NeedsRedraw )
                {
                    OnNeedsRedraw( EventArgs.Empty );
                    return;
                }
        }

        public void DeselectAll( )
        {
            foreach ( Shape shape in ShapeList )
            {
                shape.IsSelected = false;
                shape.IsActive = false;
            }
            foreach ( Connection connection in ConnectionList )
            {
                connection.IsSelected = false;
                connection.IsActive = false;
            }
            ActiveElement = null;
        }

        private void DeselectAllOthers( DiagramElement onlySelected )
        {
            foreach ( Shape shape in ShapeList )
                if ( shape != onlySelected )
                {
                    shape.IsSelected = false;
                    shape.IsActive = false;
                }
            foreach ( Connection connection in ConnectionList )
                if ( connection != onlySelected )
                {
                    connection.IsSelected = false;
                    connection.IsActive = false;
                }
        }

        private void AddCreatedShape( )
        {
            DeselectAll( );
            Shape shape = AddShape( shapeType );
            shape.Location = shapeOutline.Location;
            RecalculateSize( );
            state = State.Normal;

            shape.IsSelected = true;
            shape.IsActive = true;
            if ( shape is TypeShape ) //TODO: nem sz�p
                shape.ShowEditor( );
        }

        private void SelectElements( AbsoluteMouseEventArgs e )
        {
            DiagramElement firstElement = null;
            bool multiSelection = Control.ModifierKeys == Keys.Control;

            foreach ( DiagramElement element in GetElementsInDisplayOrder( ) )
            {
                bool isSelected = element.IsSelected;
                element.MousePressed( e );
                if ( e.Handled && ( firstElement == null ) )
                {
                    firstElement = element;
                    if ( isSelected )
                        multiSelection = true;
                }
            }

            if ( ( firstElement != null ) && !multiSelection )
                DeselectAllOthers( firstElement );

            if ( !e.Handled )
            {
                if ( !multiSelection )
                    DeselectAll( );

                if ( e.Button == MouseButtons.Left )
                {
                    state = State.Multiselecting;
                    selectionFrame.Location = e.Location;
                    selectionFrame.Size = Size.Empty;
                }
            }
        }

        private void TrySelectElements( )
        {
            selectionFrame = RectangleF.FromLTRB( Math.Min( selectionFrame.Left, selectionFrame.Right ), Math.Min( selectionFrame.Top, selectionFrame.Bottom ), Math.Max( selectionFrame.Left, selectionFrame.Right ), Math.Max( selectionFrame.Top, selectionFrame.Bottom ) );
            selectioning = true;

            foreach ( Shape shape in ShapeList )
                if ( shape.TrySelect( selectionFrame ) )
                    SelectedShapeCount++;
            foreach ( Connection connection in ConnectionList )
                if ( connection.TrySelect( selectionFrame ) )
                    SelectedConnectionCount++;

            OnSelectionChanged( EventArgs.Empty );
            OnClipboardAvailabilityChanged( EventArgs.Empty );
            OnSatusChanged( EventArgs.Empty );
            Redraw( );

            selectioning = false;
        }

        private void UpdateWindowPosition( )
        {
            if ( ActiveElement != null )
                ActiveElement.MoveWindow( );
        }

        internal void ShowWindow( PopupWindow window )
        {
            Redraw( );
            OnShowingWindow( new PopupWindowEventArgs( window ) );
        }

        internal void HideWindow( PopupWindow window )
        {
            window.Closing( );
            OnHidingWindow( new PopupWindowEventArgs( window ) );
        }

        private void AddShape( Shape shape )
        {
            shape.Diagram = this;
            shape.Modified += element_Modified;
            shape.Activating += element_Activating;
            shape.Dragging += shape_Dragging;
            shape.Resizing += shape_Resizing;
            shape.SelectionChanged += shape_SelectionChanged;
            ShapeList.AddFirst( shape );
            RecalculateSize( );
        }

        private void element_Modified( object sender, EventArgs e )
        {
            if ( !RedrawSuspended )
                RequestRedrawIfNeeded( );
            OnModified( EventArgs.Empty );
        }

        private void element_Activating( object sender, EventArgs e )
        {
            foreach ( Shape shape in ShapeList )
                if ( shape != sender )
                    shape.IsActive = false;
            foreach ( Connection connection in ConnectionList )
                if ( connection != sender )
                    connection.IsActive = false;
            ActiveElement = ( DiagramElement ) sender;
        }

        private void shape_Dragging( object sender, MoveEventArgs e )
        {
            Size offset = e.Offset;

            // Align to other shapes
            if ( Settings.Default.UsePrecisionSnapping && ( Control.ModifierKeys != Keys.Shift ) )
            {
                Shape shape = ( Shape ) sender;

                foreach ( Shape otherShape in ShapeList.GetUnselectedElements( ) )
                {
                    int xDist = otherShape.X - ( shape.X + offset.Width );
                    int yDist = otherShape.Y - ( shape.Y + offset.Height );

                    if ( Math.Abs( xDist ) <= PrecisionSize )
                    {
                        int distance1 = Math.Abs( shape.Top - otherShape.Bottom );
                        int distance2 = Math.Abs( otherShape.Top - shape.Bottom );
                        int distance = Math.Min( distance1, distance2 );

                        if ( distance <= MaximalPrecisionDistance )
                            offset.Width += xDist;
                    }
                    if ( Math.Abs( yDist ) <= PrecisionSize )
                    {
                        int distance1 = Math.Abs( shape.Left - otherShape.Right );
                        int distance2 = Math.Abs( otherShape.Left - shape.Right );
                        int distance = Math.Min( distance1, distance2 );

                        if ( distance <= MaximalPrecisionDistance )
                            offset.Height += yDist;
                    }
                }
            }

            // Get maxmimal avaiable offset for the selected elements
            foreach ( Shape shape in ShapeList )
                offset = shape.GetMaximalOffset( offset, DiagramPadding );
            foreach ( Connection connection in ConnectionList )
                offset = connection.GetMaximalOffset( offset, DiagramPadding );
            if ( !offset.IsEmpty )
            {
                foreach ( Shape shape in ShapeList.GetSelectedElements( ) )
                    shape.Offset( offset );
                foreach ( Connection connection in ConnectionList.GetSelectedElements( ) )
                    connection.Offset( offset );
            }
            RecalculateSize( );
        }

        private void shape_Resizing( object sender, ResizeEventArgs e )
        {
            if ( Settings.Default.UsePrecisionSnapping && ( Control.ModifierKeys != Keys.Shift ) )
            {
                Shape shape = ( Shape ) sender;
                Size change = e.Change;

                // Horizontal resizing
                if ( change.Width != 0 )
                    foreach ( Shape otherShape in ShapeList.GetUnselectedElements( ) )
                        if ( otherShape != shape )
                        {
                            int xDist = otherShape.Right - ( shape.Right + change.Width );
                            if ( Math.Abs( xDist ) <= PrecisionSize )
                            {
                                int distance1 = Math.Abs( shape.Top - otherShape.Bottom );
                                int distance2 = Math.Abs( otherShape.Top - shape.Bottom );
                                int distance = Math.Min( distance1, distance2 );

                                if ( distance <= MaximalPrecisionDistance )
                                {
                                    change.Width += xDist;
                                    break;
                                }
                            }
                        }

                // Vertical resizing
                if ( change.Height != 0 )
                    foreach ( Shape otherShape in ShapeList.GetUnselectedElements( ) )
                        if ( otherShape != shape )
                        {
                            int yDist = otherShape.Bottom - ( shape.Bottom + change.Height );
                            if ( Math.Abs( yDist ) <= PrecisionSize )
                            {
                                int distance1 = Math.Abs( shape.Left - otherShape.Right );
                                int distance2 = Math.Abs( otherShape.Left - shape.Right );
                                int distance = Math.Min( distance1, distance2 );

                                if ( distance <= MaximalPrecisionDistance )
                                {
                                    change.Height += yDist;
                                    break;
                                }
                            }
                        }

                e.Change = change;
            }
        }

        private void RemoveShape( Shape shape )
        {
            if ( shape.IsActive )
                ActiveElement = null;
            if ( shape.IsSelected )
            {
                SelectedShapeCount--;
                OnSelectionChanged( EventArgs.Empty );
                OnClipboardAvailabilityChanged( EventArgs.Empty );
                OnSatusChanged( EventArgs.Empty );
            }
            shape.Diagram = null;
            shape.Modified -= element_Modified;
            shape.Activating -= element_Activating;
            shape.Dragging -= shape_Dragging;
            shape.Resizing -= shape_Resizing;
            shape.SelectionChanged -= shape_SelectionChanged;
            ShapeList.Remove( shape );
            RecalculateSize( );
        }

        //TODO: legyenek ink�bb hivatkoz�sok a shape-ekhez
        private Shape GetShape( IEntity entity )
        {
            foreach ( Shape shape in ShapeList )
                if ( shape.Entity == entity )
                    return shape;
            return null;
        }

        private Connection GetConnection( Relationship relationship )
        {
            foreach ( Connection connection in ConnectionList )
                if ( connection.Relationship == relationship )
                    return connection;
            return null;
        }

        private void AddConnection( Connection connection )
        {
            connection.Diagram = this;
            connection.Modified += element_Modified;
            connection.Activating += element_Activating;
            connection.SelectionChanged += connection_SelectionChanged;
            connection.RouteChanged += connection_RouteChanged;
            connection.BendPointMove += connection_BendPointMove;
            ConnectionList.AddFirst( connection );
            RecalculateSize( );
        }

        private void RemoveConnection( Connection connection )
        {
            if ( connection.IsSelected )
            {
                SelectedConnectionCount--;
                OnSelectionChanged( EventArgs.Empty );
                OnClipboardAvailabilityChanged( EventArgs.Empty );
                OnSatusChanged( EventArgs.Empty );
            }
            connection.Diagram = null;
            connection.Modified -= element_Modified;
            connection.Activating += element_Activating;
            connection.SelectionChanged -= connection_SelectionChanged;
            connection.RouteChanged -= connection_RouteChanged;
            connection.BendPointMove -= connection_BendPointMove;
            ConnectionList.Remove( connection );
            RecalculateSize( );
        }

        private void shape_SelectionChanged( object sender, EventArgs e )
        {
            if ( !selectioning )
            {
                Shape shape = ( Shape ) sender;

                if ( shape.IsSelected )
                {
                    SelectedShapeCount++;
                    ShapeList.ShiftToFirstPlace( shape );
                }
                else
                {
                    SelectedShapeCount--;
                }

                OnSelectionChanged( EventArgs.Empty );
                OnClipboardAvailabilityChanged( EventArgs.Empty );
                OnSatusChanged( EventArgs.Empty );
            }
        }

        private void connection_SelectionChanged( object sender, EventArgs e )
        {
            if ( !selectioning )
            {
                Connection connection = ( Connection ) sender;

                if ( connection.IsSelected )
                {
                    SelectedConnectionCount++;
                    ConnectionList.ShiftToFirstPlace( connection );
                }
                else
                {
                    SelectedConnectionCount--;
                }

                OnSelectionChanged( EventArgs.Empty );
                OnClipboardAvailabilityChanged( EventArgs.Empty );
                OnSatusChanged( EventArgs.Empty );
            }
        }

        private void connection_RouteChanged( object sender, EventArgs e )
        {
            Connection connection = ( Connection ) sender;
            connection.ValidatePosition( DiagramPadding );

            RecalculateSize( );
        }

        private void connection_BendPointMove( object sender, BendPointEventArgs e )
        {
            if ( e.BendPoint.X < DiagramPadding )
                e.BendPoint.X = DiagramPadding;
            if ( e.BendPoint.Y < DiagramPadding )
                e.BendPoint.Y = DiagramPadding;

            // Snap bend points to others if possible
            if ( Settings.Default.UsePrecisionSnapping && ( Control.ModifierKeys != Keys.Shift ) )
                foreach ( Connection connection in ConnectionList.GetSelectedElements( ) )
                    foreach ( BendPoint point in connection.BendPoints )
                        if ( ( point != e.BendPoint ) && !point.AutoPosition )
                        {
                            int xDist = Math.Abs( e.BendPoint.X - point.X );
                            int yDist = Math.Abs( e.BendPoint.Y - point.Y );

                            if ( xDist <= Connection.PrecisionSize )
                                e.BendPoint.X = point.X;
                            if ( yDist <= Connection.PrecisionSize )
                                e.BendPoint.Y = point.Y;
                        }
        }

        public void CreateShape( )
        {
            CreateShape( newShapeType );
        }

        public void CreateShape( EntityType type )
        {
            state = State.CreatingShape;
            shapeType = type;
            newShapeType = type;

            switch ( type )
            {
                case EntityType.Class:
                case EntityType.Delegate:
                case EntityType.Enum:
                case EntityType.Interface:
                case EntityType.Structure:
                case EntityType.Namespace:
                    shapeOutline = TypeShape.GetOutline( Style.CurrentStyle );
                    break;

                case EntityType.Comment:
                    shapeOutline = CommentShape.GetOutline( Style.CurrentStyle );
                    break;
            }
            shapeOutline.Location = new Point( ( int ) mouseLocation.X, ( int ) mouseLocation.Y );
            Redraw( );
        }

        public Shape AddShape( EntityType type )
        {
            switch ( type )
            {
                case EntityType.Class:
                    AddClass( );
                    break;

                case EntityType.Comment:
                    AddComment( );
                    break;

                case EntityType.Delegate:
                    AddDelegate( );
                    break;

                case EntityType.Enum:
                    AddEnum( );
                    break;

                case EntityType.Interface:
                    AddInterface( );
                    break;

                case EntityType.Structure:
                    AddStructure( );
                    break;
                case EntityType.Namespace:
                    AddNamespace( );
                    break;
                default:
                    return null;
            }

            RecalculateSize( );
            return ShapeList.FirstValue;
        }

        protected override void AddNamespace( Namespace newNamespace )
        {
            base.AddNamespace( newNamespace );
            AddShape( new NamespaceShape( newNamespace ) );
        }

        protected override void AddClass( ClassType newClass )
        {
            base.AddClass( newClass );
            AddShape( new ClassShape( newClass ) );
        }

        protected override void AddStructure( StructureType structure )
        {
            base.AddStructure( structure );
            AddShape( new StructureShape( structure ) );
        }

        protected override void AddInterface( InterfaceType newInterface )
        {
            base.AddInterface( newInterface );
            InterfaceShape test = new InterfaceShape( newInterface );
            AddShape( test );
        }

        protected override void AddEnum( EnumType newEnum )
        {
            base.AddEnum( newEnum );
            AddShape( new EnumShape( newEnum ) );
        }

        protected override void AddDelegate( DelegateType newDelegate )
        {
            base.AddDelegate( newDelegate );
            AddShape( new DelegateShape( newDelegate ) );
        }

        protected override void AddComment( Comment comment )
        {
            base.AddComment( comment );
            AddShape( new CommentShape( comment ) );
        }

        public void CreateConnection( RelationshipType type )
        {
            connectionCreator = new ConnectionCreator( this, type );
            state = State.CreatingConnection;
        }

        protected override void AddAssociation( AssociationRelationship association )
        {
            base.AddAssociation( association );

            Shape startShape = GetShape( association.First );
            Shape endShape = GetShape( association.Second );
            AddConnection( new Association( association, startShape, endShape ) );
        }

        protected override void AddGeneralization( GeneralizationRelationship generalization )
        {
            base.AddGeneralization( generalization );

            Shape startShape = GetShape( generalization.First );
            Shape endShape = GetShape( generalization.Second );
            AddConnection( new Generalization( generalization, startShape, endShape ) );
        }

        protected override void AddRealization( RealizationRelationship realization )
        {
            base.AddRealization( realization );

            Shape startShape = GetShape( realization.First );
            Shape endShape = GetShape( realization.Second );
            AddConnection( new Realization( realization, startShape, endShape ) );
        }

        protected override void AddDependency( DependencyRelationship dependency )
        {
            base.AddDependency( dependency );

            Shape startShape = GetShape( dependency.First );
            Shape endShape = GetShape( dependency.Second );
            AddConnection( new Dependency( dependency, startShape, endShape ) );
        }

        protected override void AddNesting( NestingRelationship nesting )
        {
            base.AddNesting( nesting );

            Shape startShape = GetShape( nesting.First );
            Shape endShape = GetShape( nesting.Second );
            AddConnection( new Nesting( nesting, startShape, endShape ) );
        }

        protected override void AddCommentRelationship( CommentRelationship commentRelationship )
        {
            base.AddCommentRelationship( commentRelationship );

            Shape startShape = GetShape( commentRelationship.First );
            Shape endShape = GetShape( commentRelationship.Second );
            AddConnection( new CommentConnection( commentRelationship, startShape, endShape ) );
        }

        protected override void OnEntityRemoved( EntityEventArgs e )
        {
            Shape shape = GetShape( e.Entity );
            RemoveShape( shape );

            if ( e.Entity is Namespace )
            {
                Namespace.Namespaces.Remove( ( e.Entity as Namespace).Name );
                foreach ( IEntity entity in Entities )
                {
                    if ( entity is TypeBase )
                    {
                        TypeBase t = ( entity as TypeBase );
                        if ( t.ParentNameSpace == e.Entity )
                            t.ParentNameSpace = null;
                    }
                }
            }

            base.OnEntityRemoved( e );
        }

        protected override void OnRelationRemoved( RelationshipEventArgs e )
        {
            Connection connection = GetConnection( e.Relationship );
            RemoveConnection( connection );

            base.OnRelationRemoved( e );
        }

        protected override void OnDeserializing( SerializeEventArgs e )
        {
            base.OnDeserializing( e );

            // Old file format
            {
                XmlElement positionsNode = e.Node[ "Positions" ];
                if ( positionsNode != null )
                {
                    LinkedListNode< Shape > currentShapeNode = ShapeList.Last;
                    foreach ( XmlElement shapeNode in positionsNode.SelectNodes( "Shape" ) )
                    {
                        if ( currentShapeNode == null )
                            break;
                        currentShapeNode.Value.Deserialize( shapeNode );
                        currentShapeNode = currentShapeNode.Previous;
                    }

                    LinkedListNode< Connection > currentConnecitonNode = ConnectionList.Last;
                    foreach ( XmlElement connectionNode in positionsNode.SelectNodes( "Connection" ) )
                    {
                        if ( currentConnecitonNode == null )
                            break;
                        currentConnecitonNode.Value.Deserialize( connectionNode );
                        currentConnecitonNode = currentConnecitonNode.Previous;
                    }
                }
            }
        }

        protected virtual void OnOffsetChanged( EventArgs e )
        {
            if ( OffsetChanged != null )
                OffsetChanged( this, e );
            UpdateWindowPosition( );
        }

        protected virtual void OnSizeChanged( EventArgs e )
        {
            if ( SizeChanged != null )
                SizeChanged( this, e );
        }

        protected virtual void OnZoomChanged( EventArgs e )
        {
            if ( ZoomChanged != null )
                ZoomChanged( this, e );
            CloseWindows( );
        }

        protected virtual void OnSatusChanged( EventArgs e )
        {
            if ( StatusChanged != null )
                StatusChanged( this, e );
        }

        protected virtual void OnSelectionChanged( EventArgs e )
        {
            if ( SelectionChanged != null )
                SelectionChanged( this, e );
        }

        protected virtual void OnNeedsRedraw( EventArgs e )
        {
            if ( NeedsRedraw != null )
                NeedsRedraw( this, e );
        }

        protected virtual void OnClipboardAvailabilityChanged( EventArgs e )
        {
            if ( ClipboardAvailabilityChanged != null )
                ClipboardAvailabilityChanged( this, e );
        }

        protected virtual void OnShowingWindow( PopupWindowEventArgs e )
        {
            if ( ShowingWindow != null )
                ShowingWindow( this, e );
        }

        protected virtual void OnHidingWindow( PopupWindowEventArgs e )
        {
            if ( HidingWindow != null )
                HidingWindow( this, e );
        }

        private enum State
        {
            Normal,
            Multiselecting,
            CreatingShape,
            CreatingConnection,
            Dragging
        }
    }
}