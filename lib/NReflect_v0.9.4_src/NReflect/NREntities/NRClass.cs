// NReflect - Easy assembly reflection
// Copyright (C) 2010-2011 Malte Ried
//
// This file is part of NReflect.
//
// NReflect is free software: you can redistribute it and/or modify
// it under the terms of the GNU Lesser General Public License as
// published by the Free Software Foundation, either version 3 of the
// License, or (at your option) any later version.
//
// NReflect is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public License
// along with NReflect. If not, see <http://www.gnu.org/licenses/>.

using System;
using NReflect.Modifier;

namespace NReflect.NREntities
{
    /// <summary>
    ///     Represents a class which is reflected by NReflect.
    /// </summary>
    [Serializable]
    public class NRClass : NRSingleInheritanceType
    {
        // ========================================================================
        // Properties

        #region === Properties

        /// <summary>
        ///     Gets or sets the modifier for this class.
        /// </summary>
        public ClassModifier ClassModifier { get; set; }

        #endregion

        // ========================================================================
        // Methods

        #region === Methods

        /// <summary>
        ///     Accept an <see cref="IVisitor" /> instance on the implementing class and all its children.
        /// </summary>
        /// <param name="visitor">The <see cref="IVisitor" /> instance to accept.</param>
        public override void Accept( IVisitor visitor )
        {
            visitor.Visit( this );
        }

        #endregion
    }
}