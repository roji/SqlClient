// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests
{
    public static class DDAsyncTest
    {
        [SkipOnTargetFramework(TargetFrameworkMonikers.NetFramework)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void OpenConnection_WithAsyncTrue_ThrowsNotSupportedException()
        {
            //Passes on NetCore
            var asyncConnectionString = DataTestUtility.TCPConnectionString + ";async=true";
            Assert.Throws<NotSupportedException>(() => { new SqlConnection(asyncConnectionString); });
        }

        [SkipOnTargetFramework(TargetFrameworkMonikers.Netcoreapp)]
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup))]
        public static void OpenConnection_WithAsyncTrue()
        {
            // Passes on NetFx
            var asyncConnectionString = DataTestUtility.TCPConnectionString + ";async=true";
            using (SqlConnection connection = new SqlConnection(asyncConnectionString)){}
        }

        #region <<ExecuteCommand_WithNewConnection>>
        // TODO Synapse: Remove dependency on Northwind database by creating required table in setup.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ExecuteCommand_WithNewConnection_ShouldPerformAsyncByDefault()
        {
            var executedProcessList = new List<string>();

            var task1 = ExecuteCommandWithNewConnectionAsync("A", "SELECT top 10 * FROM Orders", executedProcessList);
            var task2 = ExecuteCommandWithNewConnectionAsync("B", "SELECT top 10 * FROM Products", executedProcessList);
            //wait all before verifying the results
            Task.WaitAll(task1, task2);

            //verify whether it executed async
            Assert.True(DoesProcessExecutedAsync(executedProcessList));
        }

        private static bool DoesProcessExecutedAsync(IReadOnlyList<string> executedProcessList)
        {
            for (var i = 1; i < executedProcessList.Count; i++)
            {
                if (executedProcessList[i] != executedProcessList[i - 1])
                {
                    return true;
                }
            }
            return false;
        }


        private static async Task ExecuteCommandWithNewConnectionAsync(string processName, string cmdText, ICollection<string> executedProcessList)
        {
            using (var conn = new SqlConnection(DataTestUtility.TCPConnectionString))
            {
                await conn.OpenAsync();
                var cmd = new SqlCommand(cmdText, conn);

                using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        executedProcessList.Add(processName);
                    }
                }
            }
        }
        #endregion

        #region <<ExecuteCommand_WithSharedConnection>>
        // Synapse: Parallel query execution on the same connection is not supported.
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public static void ExecuteCommand_WithSharedConnection_ShouldPerformAsyncByDefault()
        {
            var executedProcessList = new List<string>();

            //for shared connection we need to add MARS capabilities
            using (var conn = new SqlConnection((new SqlConnectionStringBuilder(DataTestUtility.TCPConnectionString) { MultipleActiveResultSets = true }).ConnectionString))
            {
                conn.Open();
                var task1 = ExecuteCommandWithSharedConnectionAsync(conn, "C", "SELECT top 10 * FROM Orders", executedProcessList);
                var task2 = ExecuteCommandWithSharedConnectionAsync(conn, "D", "SELECT top 10 * FROM Products", executedProcessList);
                //wait all before verifying the results
                Task.WaitAll(task1, task2);
            }

            //verify whether it executed async
            Assert.True(DoesProcessExecutedAsync(executedProcessList));
        }

        private static async Task ExecuteCommandWithSharedConnectionAsync(SqlConnection conn, string processName, string cmdText, ICollection<string> executedProcessList)
        {
            var cmd = new SqlCommand(cmdText, conn);

            using (SqlDataReader reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    executedProcessList.Add(processName);
                }
            }
        }
        #endregion
    }
}
