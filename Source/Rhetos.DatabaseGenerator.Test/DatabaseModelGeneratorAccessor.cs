/*
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

using Rhetos.Dsl;
using Rhetos.Extensibility;
using Rhetos.TestCommon;
using Rhetos.Utilities;
using System;
using System.Collections.Generic;

namespace Rhetos.DatabaseGenerator.Test
{
    public class DatabaseModelGeneratorAccessor : DatabaseModelGenerator, ITestAccessor
    {
        public DatabaseModelGeneratorAccessor(
            IPluginsContainer<IConceptDatabaseDefinition> plugins,
            IDslModel dslModel,
            IDatabaseModelFile databaseModelFile = null)
            : base(plugins, dslModel, new ConsoleLogProvider(), databaseModelFile,
                  new DatabaseModelGeneratorDependencies(new ConsoleLogProvider()))
        {
        }

        //public List<ConceptApplication> CreateNewApplications()
        //{
        //    return (List<ConceptApplication>)this.Invoke("CreateNewApplications");
        //}

        public static string GetCodeGeneratorSeparator(int codeGeneratorId)
        {
            return (string)TestAccessorHelpers.Invoke<DatabaseModelGenerator>("GetCodeGeneratorSeparator", codeGeneratorId);
        }

        public static Dictionary<int, string> ExtractCreateQueries(string generatedSqlCode)
        {
            return (Dictionary<int, string>)TestAccessorHelpers.Invoke<DatabaseModelGenerator>("ExtractCreateQueries", generatedSqlCode);
        }

        //public static IEnumerable<CodeGeneratorDependency> ExtractDependenciesFromConceptInfos(
        //    IEnumerable<CodeGenerator> newConceptApplications)
        //{
        //    return (IEnumerable<CodeGeneratorDependency>)TestAccessorHelpers
        //        .Invoke<DatabaseModelGenerator>("ExtractDependenciesFromConceptInfos", newConceptApplications);
        //}

        //public void ComputeDependsOn(IEnumerable<CodeGenerator> newConceptApplications)
        //{
        //    this.Invoke("ComputeDependsOn", newConceptApplications);
        //}
    }
}
