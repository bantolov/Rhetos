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

using Autofac;
using Rhetos;
using Rhetos.MsSqlEf6.CommonConcepts;
using Rhetos.Utilities;
using System.ComponentModel.Composition;

namespace TestAppDirectMsSqlEf6
{
    [Export(typeof(Module))]
    public class MultiTenantAutofacModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            ExecutionStage stage = builder.GetRhetosExecutionStage();

            if (stage.IsDatabaseUpdate || stage.IsApplicationInitialization)
                builder.Register(GetConnectionStringForDbUpdate).As<ConnectionString>().SingleInstance();
            else
            {
                builder.Register(GetConnectionStringForUser).As<ConnectionString>().InstancePerMatchingLifetimeScope(UnitOfWorkScope.ScopeName);
                builder.Register(GetEf6InitializationConnectionString).SingleInstance();
            }
        }

        const string dbUpdateTenantConfigurationKey = "Rhetos:DbUpdate:Tenant";
        const string runtimeInitializationConnectionStringConfigurationKey = "MultitenantCommonInitializationConnectionString";

        /// <summary>
        /// On deployment, admin can specify in configuration (e.g. in a local environment variable) the tenant name, or the database connection string.
        /// </summary>
        private static ConnectionString GetConnectionStringForDbUpdate(IComponentContext context)
        {
            // 1. If the dbupdate is called for a specific tenant, update the tenant's database.
            // For example, DbUpdate can be configured by setting the environment variable in command line before running rhetos dbupdate: SET Rhetos__DbUpdate__Tenant=...
            var configuration = context.Resolve<IConfiguration>();
            var tenant = configuration.GetValue<string>(dbUpdateTenantConfigurationKey);
            if (!string.IsNullOrEmpty(tenant))
                return GetTenantsConnectionString($"DB_UPDATE_TENANT_{tenant}");

            // 2. Otherwise, use the default database, if provided.
            var sqlUtility = context.Resolve<ISqlUtility>();
            var rhetosConnectionString = new ConnectionString(configuration, sqlUtility);
            if (!string.IsNullOrEmpty(rhetosConnectionString))
                return rhetosConnectionString;

            throw new ArgumentException($"The database connection string is not specified. Please set the configuration option '{dbUpdateTenantConfigurationKey}' or '{ConnectionString.ConnectionStringConfigurationKey}'.");
        }

        /// <summary>
        /// In runtime, from user context we can get the tenant's connection string.
        /// </summary>
        private static ConnectionString GetConnectionStringForUser(IComponentContext context)
        {
            var user = context.Resolve<IUserInfo>();
            var tenant = $"REQUEST_TENANT_{user.UserName}";
            return GetTenantsConnectionString(tenant);
        }

        private static ConnectionString GetTenantsConnectionString(string tenant)
        {
            return new ConnectionString($"CONNECTION_STRING_FOR_{tenant}");
        }

        private Ef6InitializationConnectionString GetEf6InitializationConnectionString(IComponentContext context)
        {
            var configuration = context.Resolve<IConfiguration>();
            var sqlUtility = context.Resolve<ISqlUtility>();
            var rhetosConnectionString = new ConnectionString(ConnectionString.CreateConnectionString(configuration, sqlUtility, runtimeInitializationConnectionStringConfigurationKey));
            if (string.IsNullOrEmpty(rhetosConnectionString))
                throw new ArgumentException($"The database connection string is not specified. Please set the configuration option '{runtimeInitializationConnectionStringConfigurationKey}'.");
            return new Ef6InitializationConnectionString(rhetosConnectionString);
        }
    }
}
