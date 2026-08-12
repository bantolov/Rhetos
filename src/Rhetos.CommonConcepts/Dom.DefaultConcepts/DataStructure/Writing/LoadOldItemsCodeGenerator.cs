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

using Rhetos.Compiler;
using Rhetos.Dsl;
using Rhetos.Dsl.DefaultConcepts;
using Rhetos.Extensibility;
using System.ComponentModel.Composition;

namespace Rhetos.Dom.DefaultConcepts
{
    [Export(typeof(IConceptCodeGenerator))]
    [ExportMetadata(MefProvider.Implements, typeof(LoadOldItemsInfo))]
    public class LoadOldItemsCodeGenerator : IConceptCodeGenerator
    {
        public static readonly CsTag<LoadOldItemsInfo> SelectPropertiesTag = "SelectProperties";

        public void GenerateCode(IConceptInfo conceptInfo, ICodeBuilder codeBuilder)
        {
            var info = (LoadOldItemsInfo)conceptInfo;

            // DomHelper.ToListOrEmpty skips the query execution when the IDs list is empty (on a save that only
            // inserts or only updates the records), to avoid compiling the query expression to IL code (a temporary
            // DynamicMethod) on each Save, even though there are no records to read.
            string snippet =
            $@"var updatedIdsList = updatedNew.Select(item => item.ID).ToList();
            var deletedIdsList = deletedIds.Select(item => item.ID).ToList();
            var updatedOld = DomHelper.ToListOrEmpty(Filter(Query(), updatedIdsList).Select(item => new {{ item.ID{SelectPropertiesTag.Evaluate(info)} }}), updatedIdsList.Count > 0);
            var deletedOld = DomHelper.ToListOrEmpty(Filter(Query(), deletedIdsList).Select(item => new {{ item.ID{SelectPropertiesTag.Evaluate(info)} }}), deletedIdsList.Count > 0);
            Rhetos.Utilities.Graph.SortByGivenOrder(updatedOld, updatedIdsList, item => item.ID);
            Rhetos.Utilities.Graph.SortByGivenOrder(deletedOld, deletedIdsList, item => item.ID);

            ";
            codeBuilder.InsertCode(snippet, WritableOrmDataStructureCodeGenerator.InitializationTag, info.SaveMethod.Entity);
        }
    }
}
