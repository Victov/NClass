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

namespace NClass.Core
{
    public abstract class Element : IModifiable
    {
        private int dontRaiseRequestCount;

        protected bool Initializing { get; set; } = false;

        protected bool RaiseChangedEvent
        {
            get { return dontRaiseRequestCount == 0; }
            set
            {
                if ( !value )
                    dontRaiseRequestCount++;
                else if ( dontRaiseRequestCount > 0 )
                    dontRaiseRequestCount--;

                if ( RaiseChangedEvent && IsDirty )
                    OnModified( EventArgs.Empty );
            }
        }

        public event EventHandler Modified;

        public bool IsDirty { get; private set; }

        public virtual void Clean( )
        {
            IsDirty = false;
            //TODO: tagok tisztítása
        }

        protected void Changed( )
        {
            if ( !Initializing )
                if ( RaiseChangedEvent )
                    OnModified( EventArgs.Empty );
                else
                    IsDirty = true;
        }

        private void OnModified( EventArgs e )
        {
            IsDirty = true;
            if ( Modified != null )
                Modified( this, e );
        }
    }
}