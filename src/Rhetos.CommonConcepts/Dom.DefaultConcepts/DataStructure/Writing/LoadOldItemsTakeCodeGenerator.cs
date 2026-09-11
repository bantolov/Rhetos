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
using Rhetos.DatabaseGenerator.DefaultConcepts;
using Rhetos.Dsl;
using Rhetos.Dsl.DefaultConcepts;
using Rhetos.Extensibility;
using System.ComponentModel.Composition;

namespace Rhetos.Dom.DefaultConcepts
{
    [Export(typeof(IConceptCodeGenerator))]
    [ExportMetadata(MefProvider.Implements, typeof(LoadOldItemsTakeInfo))]
    public class LoadOldItemsTakeCodeGenerator : IConceptCodeGenerator
    {
        private readonly IDslModel _dslModel;
        private readonly ConceptMetadata _conceptMetadata;

        public LoadOldItemsTakeCodeGenerator(IDslModel dslModel, ConceptMetadata conceptMetadata)
        {
            _dslModel = dslModel;
            _conceptMetadata = conceptMetadata;
        }

        public void GenerateCode(IConceptInfo conceptInfo, ICodeBuilder codeBuilder)
        {
            var info = (LoadOldItemsTakeInfo)conceptInfo;
            string propertyName = info.GetPropertyName();

            codeBuilder.InsertCode(
                $",\r\n                {propertyName} = item.{info.Path}",
                LoadOldItemsCodeGenerator.SelectPropertiesTag, info.LoadOldItems);

            codeBuilder.InsertCode(
                $"\r\n            public {GetCsPropertyType(info)} {propertyName} {{ get; set; }}",
                LoadOldItemsCodeGenerator.OldItemPropertiesTag, info.LoadOldItems);
        }

        /// <summary>
        /// Returns the C# type of the property selected by the path, as declared in the entity's queryable class.
        /// </summary>
        private string GetCsPropertyType(LoadOldItemsTakeInfo info)
        {
            var property = DslUtility.GetPropertyByPath(info.LoadOldItems.SaveMethod.Entity, info.Path, _dslModel);
            if (property.IsError)
                throw new DslConceptSyntaxException(info, "Invalid path: " + property.Error);

            // A path that ends with a reference property name selects the navigation property.
            // A path that ends with the reference's ID property resolves to GuidPropertyInfo instead.
            if (property.Value is ReferencePropertyInfo reference)
            {
                if (!DslUtility.IsQueryable(reference.Referenced))
                    throw new DslConceptSyntaxException(info, $"The path ends with {reference.GetUserDescription()}," +
                        $" but there is no navigation property because {reference.Referenced.GetUserDescription()} is not queryable." +
                        $" Use the reference ID property instead ('{info.Path}ID').");
                return $"Common.Queryable.{reference.Referenced.Module.Name}_{reference.Referenced.Name}";
            }

            string csPropertyType = _conceptMetadata.GetCsPropertyType(property.Value);
            if (string.IsNullOrEmpty(csPropertyType))
                throw new DslConceptSyntaxException(info, $"{property.Value.GetKeywordOrTypeName()} is not supported" +
                    $" for {info.GetKeywordOrTypeName()}, because it does not provide concept metadata for C# property type.");
            return csPropertyType;
        }
    }
}
