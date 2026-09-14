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
using System.Collections.Concurrent;
using System.ComponentModel.Composition;

namespace Rhetos.Dom.DefaultConcepts
{
    [Export(typeof(IConceptCodeGenerator))]
    [ExportMetadata(MefProvider.Implements, typeof(KeepSynchronizedOnChangedItemsInfo))]
    public class KeepSynchronizedOnChangedItemsCodeGenerator : IConceptCodeGenerator
    {
        public static string OverrideRecomputeTag(KeepSynchronizedOnChangedItemsInfo info)
        {
            return string.Format("/*OverrideRecompute {0}.{1}*/",
                info.KeepSynchronized.GetKey(),
                info.UpdateOnChange.GetAlternativeKey());
        }

        public void GenerateCode(IConceptInfo conceptInfo, ICodeBuilder codeBuilder)
        {
            var info = (KeepSynchronizedOnChangedItemsInfo) conceptInfo;

            string uniqueName = GetUniqueNameSuffix(info);

            codeBuilder.InsertCode(
                FilterOldItemsBeforeSaveSnippet(info.UpdateOnChange.DependsOn, info.UpdateOnChange.FilterType, info.UpdateOnChange.FilterFormula, uniqueName),
                WritableOrmDataStructureCodeGenerator.OldDataLoadedTag, info.UpdateOnChange.DependsOn);

            codeBuilder.InsertCode(
                FilterAndRecomputeAfterSave(info, info.UpdateOnChange.FilterType, uniqueName),
                WritableOrmDataStructureCodeGenerator.OnSaveTag1, info.UpdateOnChange.DependsOn);
        }

        private static readonly ConcurrentDictionary<(string, string), int> _uniqueNumberByDependsOnAndTarget = new ConcurrentDictionary<(string, string), int>();

        /// <summary>
        /// The generated variables are placed in "DependsOn" repository. They contain "Target" in the name,
        /// and additional disambiguation is made by additional counter by DependsOn and Target.
        /// </summary>
        private static string GetUniqueNameSuffix(KeepSynchronizedOnChangedItemsInfo info)
        {
            var key = (info.UpdateOnChange.DependsOn.GetKey(), info.KeepSynchronized.EntityComputedFrom.Target.GetKey());
            int uniqueNumber = _uniqueNumberByDependsOnAndTarget.AddOrUpdate(key, 1, (_, oldValue) => oldValue + 1);
            return DslUtility.NameOptionalModule(info.KeepSynchronized.EntityComputedFrom.Target, info.UpdateOnChange.DependsOn.Module)
                + uniqueNumber;
        }

        /// <remarks>
        /// The filter is not created if there are no old items, to avoid the filter's overhead on a save that only inserts the records.
        /// The filter formula often executes a query over the changed items (see <see cref="ChangesOnReferencedInfo"/>, for example),
        /// and the in-memory query would compile its expression tree to IL code (a temporary DynamicMethod) on each execution,
        /// even though there are no records to read.
        /// The materialized <c>updatedNew</c> and <c>deletedIds</c> lists are checked instead of <c>updated</c> and <c>deleted</c>,
        /// because the latter are lazy (see <see cref="DomHelper.LazyLoadData"/>) and checking them would load the old data
        /// even for the filter formulas that do not use the changed items.
        /// </remarks>
        private static string FilterOldItemsBeforeSaveSnippet(DataStructureInfo hookOnSaveEntity, string filterType, string filterFormula, string uniqueName)
        {
            return
            $@"Func<IEnumerable<{hookOnSaveEntity.Module.Name}.{hookOnSaveEntity.Name}>, {filterType}> filterLoadKeepSynchronizedOnChangedItems{uniqueName} =
                {filterFormula};
            bool hasKeepSynchronizedOnChangedItems{uniqueName}Old = updatedNew.Any() || deletedIds.Any();
            {filterType} filterKeepSynchronizedOnChangedItems{uniqueName}Old = hasKeepSynchronizedOnChangedItems{uniqueName}Old
                ? filterLoadKeepSynchronizedOnChangedItems{uniqueName}(updated.Concat(deleted))
                : default;

            ";
        }

        private static string FilterAndRecomputeAfterSave(KeepSynchronizedOnChangedItemsInfo info, string filterType, string uniqueName)
        {
            string recomputeMethodName =
                info.KeepSynchronized.EntityComputedFrom.Target.Module.Name
                + "." + info.KeepSynchronized.EntityComputedFrom.Target.Name
                + "." + info.KeepSynchronized.EntityComputedFrom.RecomputeFunctionName();

            return
                $@"{OverrideRecomputeTag(info)}
                {{
                    bool hasNew = inserted.Any() || updated.Any();
                    {filterType} filteredNew = hasNew
                        ? filterLoadKeepSynchronizedOnChangedItems{uniqueName}(inserted.Concat(updated))
                        : default;
                    if (hasKeepSynchronizedOnChangedItems{uniqueName}Old && hasNew
                        && KeepSynchronizedHelper.OptimizeFiltersUnion(filteredNew, filterKeepSynchronizedOnChangedItems{uniqueName}Old, out {filterType} optimizedFilter))
                        _domRepository.{recomputeMethodName}(optimizedFilter);
                    else
                    {{
                        if (hasKeepSynchronizedOnChangedItems{uniqueName}Old)
                            _domRepository.{recomputeMethodName}(filterKeepSynchronizedOnChangedItems{uniqueName}Old);
                        if (hasNew)
                            _domRepository.{recomputeMethodName}(filteredNew);
                    }}
                }}

                ";
        }
    }
}
