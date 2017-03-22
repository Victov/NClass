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
using System.Runtime.InteropServices;
using System.Xml;
using NClass.Translations;

namespace NClass.Core
{
    public class Namespace : Element, IEntity
    {
        public string Name { get; set; }

        public static Dictionary<string, Namespace> Namespaces = new Dictionary< string, Namespace >();

        internal Namespace( ) {}

        public Namespace( string name )
        {
            this.Name = name;
            while (Namespaces.ContainsKey(Name))
            {
                Name = "New" + Name;
            }
            Namespaces.Add( Name, this );
        }

        public void SetName( string newname )
        {
            if (Namespaces.ContainsValue(this))
                Namespaces.Remove(Name);
            this.Name = newname;
            Namespaces.Add( Name, this );
        }

        public event SerializeEventHandler Serializing;
        public event SerializeEventHandler Deserializing;

        public EntityType EntityType
        {
            get { return EntityType.Namespace; }
        }

        void ISerializableElement.Serialize( XmlElement node )
        {
            Serialize( node );
        }

        void ISerializableElement.Deserialize( XmlElement node )
        {
            Deserialize( node );
        }

        public Namespace Clone( )
        {
            return new Namespace( Name );
        }

        /// <exception cref="ArgumentNullException">
        ///     <paramref name="node" /> is null.
        /// </exception>
        internal void Serialize( XmlElement node )
        {
            if ( node == null )
                throw new ArgumentNullException( "node" );

            XmlElement child = node.OwnerDocument.CreateElement( "NamespaceName" );
            child.InnerText = Name;
            node.AppendChild( child );

            OnSerializing( new SerializeEventArgs( node ) );
        }

        /// <exception cref="BadSyntaxException">
        ///     An error occured while deserializing.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     The XML document is corrupt.
        /// </exception>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="node" /> is null.
        /// </exception>
        internal void Deserialize( XmlElement node )
        {
            if ( node == null )
                throw new ArgumentNullException( "node" );

            XmlElement textNode = node[ "NamespaceName" ];

            if (Namespaces.ContainsValue(this))
                Namespaces.Remove(Name);

            if ( textNode != null )
                Name = textNode.InnerText;
            else
                Name = "";
            
            Namespaces.Add(Name, this);

            OnDeserializing( new SerializeEventArgs( node ) );
        }

        private void OnSerializing( SerializeEventArgs e )
        {
            if ( Serializing != null )
                Serializing( this, e );
        }

        private void OnDeserializing( SerializeEventArgs e )
        {
            if ( Deserializing != null )
                Deserializing( this, e );
        }

        public override string ToString( )
        {
            const int MaxLength = 50;

            if ( Name == "" )
                return "";
            if ( Name.Length > MaxLength )
                return '"' + Name.Substring( 0, MaxLength ) + "...\"";
            return '"' + Name + '"';
        }
    }
}