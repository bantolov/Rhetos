﻿/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using Rhetos.Utilities;
using System.Collections.Generic;

namespace Rhetos.Dsl
{
    public interface IExternalTextReader
    {
        ValueOrError<string> Read(DslScript dslScript, string relativePathOrResourceName);

        /// <summary>
        /// Returns the list of files that have been read by the <see cref="IExternalTextReader"/>.
        /// These are the additional source files with the external code snippets (for example a SQL scripts), that were referenced from the .rhe scripts.
        /// </summary>
        IReadOnlyCollection<string> ExternalFiles { get; }
    }
}
