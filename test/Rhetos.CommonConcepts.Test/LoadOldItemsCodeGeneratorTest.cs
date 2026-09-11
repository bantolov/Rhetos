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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Rhetos.CommonConcepts.Test.Mocks;
using Rhetos.Compiler;
using Rhetos.DatabaseGenerator.DefaultConcepts;
using Rhetos.Dom.DefaultConcepts;
using Rhetos.Dsl;
using Rhetos.Dsl.DefaultConcepts;
using Rhetos.TestCommon;
using Rhetos.Utilities;

namespace Rhetos.CommonConcepts.Test
{
    [TestClass]
    public class LoadOldItemsCodeGeneratorTest
    {
        [TestMethod]
        public void OldItemClassAndQuery()
        {
            string generatedCode = GenerateCode("Name");

            TestUtility.AssertContains(generatedCode, [
                "public class OldItem : IEntity",
                "public Guid ID { get; set; }",
                "var updatedOld = DomHelper.ToListOrEmpty(Filter(Query(), updatedIdsList).Select(item => new OldItem { ID = item.ID,",
                "var deletedOld = DomHelper.ToListOrEmpty(Filter(Query(), deletedIdsList).Select(item => new OldItem { ID = item.ID,",
                "}), updatedIdsList.Count > 0);",
                "}), deletedIdsList.Count > 0);",
            ]);
        }

        [TestMethod]
        public void TakePropertyType()
        {
            var tests = new (string Path, string ExpectedCsType, string ExpectedPropertyName)[]
            {
                ("Name", "string", "Name"),
                ("Code", "int?", "Code"),
                ("ParentID", "Guid?", "ParentID"), // The reference's ID property.
                ("Parent", "Common.Queryable.TestModule_Parent", "Parent"), // The reference's navigation property.
                ("Parent.Name", "string", "ParentName"),
                ("Parent.ID", "Guid?", "ParentID"),
                ("Base.Name", "string", "BaseName"),
            };

            foreach (var test in tests)
            {
                string generatedCode = GenerateCode(test.Path);

                TestUtility.AssertContains(generatedCode, [
                    $"public {test.ExpectedCsType} {test.ExpectedPropertyName} {{ get; set; }}",
                    $"{test.ExpectedPropertyName} = item.{test.Path}"],
                    message: test.Path);
            }
        }

        [TestMethod]
        public void TakeUnsupportedPropertyType()
        {
            TestUtility.ShouldFail<DslConceptSyntaxException>(
                () => GenerateCode("Children"),
                "LinkedItems", "does not provide concept metadata for C# property type");
        }

        private static string GenerateCode(string takePath)
        {
            var module = new ModuleInfo { Name = "TestModule" };
            var entity = new EntityInfo { Module = module, Name = "Simple" };
            var parent = new EntityInfo { Module = module, Name = "Parent" };
            var child = new EntityInfo { Module = module, Name = "Child" };
            var baseEntity = new EntityInfo { Module = module, Name = "BaseEntity" };
            var childReference = new ReferencePropertyInfo { DataStructure = child, Name = "Simple", Referenced = entity };
            var saveMethod = new SaveMethodInfo { Entity = entity };
            var loadOldItems = new LoadOldItemsInfo { SaveMethod = saveMethod };
            var take = new LoadOldItemsTakeInfo { LoadOldItems = loadOldItems, Path = takePath };

            var dslModel = new DslModelMock
            {
                module, entity, parent, child, baseEntity,
                new ShortStringPropertyInfo { DataStructure = entity, Name = "Name" },
                new IntegerPropertyInfo { DataStructure = entity, Name = "Code" },
                new ReferencePropertyInfo { DataStructure = entity, Name = "Parent", Referenced = parent },
                new LinkedItemsInfo { DataStructure = entity, Name = "Children", ReferenceProperty = childReference },
                new ShortStringPropertyInfo { DataStructure = parent, Name = "Name" },
                childReference,
                new UniqueReferenceInfo { Extension = entity, Base = baseEntity },
                new ShortStringPropertyInfo { DataStructure = baseEntity, Name = "Name" },
                saveMethod, loadOldItems, take,
            };

            var conceptMetadata = new ConceptMetadata(
                new PluginsContainerMock<IConceptMetadataExtension>(
                    new ShortStringCsPropertyType(), new IntegerCsPropertyType(), new GuidCsPropertyType(), new ReferenceCsPropertyType()),
                new ConsoleLogProvider());

            var codeBuilder = new CodeBuilder("/*", "*/");
            codeBuilder.InsertCode(RepositoryHelper.RepositoryMembers.Evaluate(entity) + WritableOrmDataStructureCodeGenerator.InitializationTag.Evaluate(entity));
            new LoadOldItemsCodeGenerator().GenerateCode(loadOldItems, codeBuilder);
            new LoadOldItemsTakeCodeGenerator(dslModel, conceptMetadata).GenerateCode(take, codeBuilder);
            return codeBuilder.GenerateCode();
        }
    }
}
