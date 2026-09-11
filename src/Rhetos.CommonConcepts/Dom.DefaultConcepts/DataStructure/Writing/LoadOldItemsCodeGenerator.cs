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
        /// <summary>
        /// Name of the generated class that holds the old version of the data: the ID property and the properties
        /// selected by <see cref="LoadOldItemsTakeInfo"/>. The class is nested in the entity's repository class,
        /// so the Save method code (OnSaveUpdate, OnSaveValidate and similar concepts) can reference it by the simple name.
        /// </summary>
        public const string OldItemClassName = "OldItem";

        /// <summary>Property assignments in the query projection to the old items, for example ", Name = item.Name".</summary>
        public static readonly CsTag<LoadOldItemsInfo> SelectPropertiesTag = "SelectProperties";

        /// <summary>Property declarations in the old item class, for example "public string Name { get; set; }".</summary>
        public static readonly CsTag<LoadOldItemsInfo> OldItemPropertiesTag = "OldItemProperties";

        public void GenerateCode(IConceptInfo conceptInfo, ICodeBuilder codeBuilder)
        {
            var info = (LoadOldItemsInfo)conceptInfo;

            string oldItemClassSnippet =
            $@"public class {OldItemClassName} : IEntity
        {{
            public Guid ID {{ get; set; }}{OldItemPropertiesTag.Evaluate(info)}
        }}

        ";
            codeBuilder.InsertCode(oldItemClassSnippet, RepositoryHelper.RepositoryMembers, info.SaveMethod.Entity);

            // DomHelper.ToListOrEmpty skips the query execution when the IDs list is empty (on a save that only
            // inserts or only updates the records), to avoid compiling the query expression to IL code (a temporary
            // DynamicMethod) on each Save, even though there are no records to read.
            string loadOldItemsSnippet =
            $@"var updatedIdsList = updatedNew.Select(item => item.ID).ToList();
            var deletedIdsList = deletedIds.Select(item => item.ID).ToList();
            var updatedOld = DomHelper.ToListOrEmpty(Filter(Query(), updatedIdsList).Select(item => new {OldItemClassName} {{ ID = item.ID{SelectPropertiesTag.Evaluate(info)} }}), updatedIdsList.Count > 0);
            var deletedOld = DomHelper.ToListOrEmpty(Filter(Query(), deletedIdsList).Select(item => new {OldItemClassName} {{ ID = item.ID{SelectPropertiesTag.Evaluate(info)} }}), deletedIdsList.Count > 0);
            Rhetos.Utilities.Graph.SortByGivenOrder(updatedOld, updatedIdsList, item => item.ID);
            Rhetos.Utilities.Graph.SortByGivenOrder(deletedOld, deletedIdsList, item => item.ID);

            ";
            codeBuilder.InsertCode(loadOldItemsSnippet, WritableOrmDataStructureCodeGenerator.InitializationTag, info.SaveMethod.Entity);
        }
    }
}
