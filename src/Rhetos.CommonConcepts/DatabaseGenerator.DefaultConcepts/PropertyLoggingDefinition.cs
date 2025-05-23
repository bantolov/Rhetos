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

using Rhetos.Compiler;
using Rhetos.Dsl;
using Rhetos.Dsl.DefaultConcepts;
using Rhetos.Extensibility;
using Rhetos.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;

namespace Rhetos.DatabaseGenerator.DefaultConcepts
{
#pragma warning disable CS0618 // Type or member is obsolete
    [Export(typeof(IConceptDatabaseDefinition))]
    [ExportMetadata(MefProvider.Implements, typeof(PropertyLoggingInfo))]
    public class PropertyLoggingDefinition : IConceptDatabaseDefinitionExtension
#pragma warning restore CS0618 // Type or member is obsolete
    {
        private readonly ConceptMetadata _conceptMetadata;

        protected ISqlResources Sql { get; private set; }

        protected ISqlUtility SqlUtility { get; private set; }

        public PropertyLoggingDefinition(ConceptMetadata conceptMetadata, ISqlResources sqlResources, ISqlUtility sqlUtility)
        {
            _conceptMetadata = conceptMetadata;
            this.Sql = sqlResources;
            this.SqlUtility = sqlUtility;
        }

        public void ExtendDatabaseStructure(IConceptInfo conceptInfo, ICodeBuilder codeBuilder, out IEnumerable<Tuple<IConceptInfo, IConceptInfo>> createdDependencies)
        {
            var info = (PropertyLoggingInfo)conceptInfo;

            var column = _conceptMetadata.GetColumnName(info.Property);
            var columnType = _conceptMetadata.GetColumnType(info.Property);

            if (column == null || columnType == null)
            {
                createdDependencies = null;
                return; // Only simple database columns are logged by this concept.
            }

            string propertyToStringSnippet = DslUtility.FindSqlResourceKeyPropertyType(Sql, "PropertyLoggingDefinition_TextValue_", info.Property).SqlScript;
            if (string.IsNullOrEmpty(propertyToStringSnippet))
                propertyToStringSnippet = Sql.Get("PropertyLoggingDefinition_TextValue");
            var propertyToString = string.Format(propertyToStringSnippet, column);

            codeBuilder.InsertCode(
                Sql.Format("PropertyLoggingDefinition_GenericPropertyLogging",
                    column,
                    propertyToString),
                EntityLoggingDefinition.LogPropertyTag, info.EntityLogging);

            string propertyDeletedLoggingSql = Sql.TryFormat("PropertyLoggingDefinition_GenericPropertyDeletedLogging", column, propertyToString);
            if (propertyDeletedLoggingSql != null)
                codeBuilder.InsertCode(
                    propertyDeletedLoggingSql,
                    EntityLoggingDefinition.LogPropertyDeletedTag, info.EntityLogging);

            createdDependencies = [Tuple.Create<IConceptInfo, IConceptInfo>(info.Property, info.EntityLogging)]; // Entity's property (table column) must be created before entity's logging trigger is created.
        }

        public string CreateDatabaseStructure(IConceptInfo conceptInfo)
        {
            return null;
        }

        public string RemoveDatabaseStructure(IConceptInfo conceptInfo)
        {
            return null;
        }
    }
}