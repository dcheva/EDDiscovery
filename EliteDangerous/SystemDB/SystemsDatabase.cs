﻿using SQLLiteExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;

namespace EliteDangerousCore.DB
{
    public class SystemsDatabaseConnection : IDisposable
    {
        internal SQLiteConnectionSystem Connection { get; private set; }

        public SystemsDatabaseConnection()
        {
            Connection = new SQLiteConnectionSystem();
        }

        public void Dispose()
        {
            if (Connection != null)
            {
                Connection.Dispose();
                Connection = null;
            }
        }
    }

    public class SystemsDatabase : SQLProcessingThread<SystemsDatabaseConnection>
    {
        private SystemsDatabase()
        {
        }

        public static SystemsDatabase Instance { get; } = new SystemsDatabase();

        public void Initialize()
        {
            ExecuteWithDatabase(cn => { cn.Connection.UpgradeSystemsDB(); });
        }

        const string TempTablePostfix = "temp"; // postfix for temp tables
        //const string DebugOutfile = @"c:\code\edsm\Jsonprocess.lst";        // null off
        const string DebugOutfile = null;

        public long UpgradeSystemTableFromFile(string filename, bool[] gridids, Func<bool> cancelRequested, Action<string> reportProgress)
        {
            ExecuteWithDatabase( action: conn =>
            {
                conn.Connection.DropStarTables(TempTablePostfix);     // just in case, kill the old tables
                conn.Connection.CreateStarTables(TempTablePostfix);     // and make new temp tables
            });

            DateTime maxdate = DateTime.MinValue;
            long updates = SystemsDB.ParseEDSMJSONFile(filename, gridids, ref maxdate, cancelRequested, reportProgress, TempTablePostfix, presumeempty: true, debugoutputfile: DebugOutfile);

            if (updates > 0)
            {
                ExecuteWithDatabase(action: conn =>
                {
                    //RebuildRunning = true;

                    reportProgress?.Invoke("Remove old data");
                    conn.Connection.DropStarTables();     // drop the main ones - this also kills the indexes

                    conn.Connection.RenameStarTables(TempTablePostfix, "");     // rename the temp to main ones

                    reportProgress?.Invoke("Shrinking database");
                    conn.Connection.Vacuum();

                    reportProgress?.Invoke("Creating indexes");
                    conn.Connection.CreateSystemDBTableIndexes();

                    //RebuildRunning = false;
                });

                SetLastEDSMRecordTimeUTC(maxdate);          // record last data stored in database

                return updates;
            }
            else
            {
                ExecuteWithDatabase(action: conn =>
                {
                    conn.Connection.DropStarTables(TempTablePostfix);     // clean out half prepared tables
                });

                return -1;
            }
        }

        public void UpgradeSystemTableFrom102TypeDB(Func<bool> cancelRequested, Action<string> reportProgress, bool fullsyncrequested)
        {
            bool executeupgrade = false;

            // first work out if we can upgrade, if so, create temp tables

            ExecuteWithDatabase(action: conn =>
            {
                var list = conn.Connection.Tables();

                if (list.Contains("EdsmSystems"))
                {
                    conn.Connection.DropStarTables(TempTablePostfix);     // just in case, kill the old tables
                    conn.Connection.CreateStarTables(TempTablePostfix);     // and make new temp tables
                    executeupgrade = true;
                }
            });

            //drop connection, execute upgrade in another connection, this solves an issue with SQL 17 error

            if (executeupgrade)
            {
                if (!fullsyncrequested)     // if we did not request a full upgrade, we can use the current data and transmute
                {
                    int maxgridid = int.MaxValue;// 109;    // for debugging

                    long updates = SystemsDB.UpgradeDB102to200(cancelRequested, reportProgress, TempTablePostfix, tablesareempty: true, maxgridid: maxgridid);

                    ExecuteWithDatabase(action: conn =>
                    {
                        if (updates >= 0) // a cancel will result in -1
                        {
                            //RebuildRunning = true;

                            // keep code for checking

                            //if (false)   // demonstrate replacement to show rows are overwitten and not duplicated in the edsmid column and that speed is okay
                            //{
                            //    long countrows = conn.CountOf("Systems" + tablepostfix, "edsmid");
                            //    long countnames = conn.CountOf("Names" + tablepostfix, "id");
                            //    long countsectors = conn.CountOf("Sectors" + tablepostfix, "id");

                            //    // replace takes : Sector 108 took 44525 U1 + 116 store 5627 total 532162 0.02061489 cumulative 11727

                            //    SystemsDB.UpgradeDB102to200(cancelRequested, reportProgress, tablepostfix, tablesareempty: false, maxgridid: maxgridid);
                            //    System.Diagnostics.Debug.Assert(countrows == conn.CountOf("Systems" + tablepostfix, "edsmid"));
                            //    System.Diagnostics.Debug.Assert(countnames * 2 == conn.CountOf("Names" + tablepostfix, "id"));      // names are duplicated.. so should be twice as much
                            //    System.Diagnostics.Debug.Assert(countsectors == conn.CountOf("Sectors" + tablepostfix, "id"));
                            //    System.Diagnostics.Debug.Assert(1 == conn.CountOf("Systems" + tablepostfix, "edsmid", "edsmid=6719254"));
                            //}

                            conn.Connection.DropStarTables();     // drop the main ones - this also kills the indexes

                            conn.Connection.RenameStarTables(TempTablePostfix, "");     // rename the temp to main ones

                            reportProgress?.Invoke("Removing old system tables");

                            conn.Connection.ExecuteNonQueries(new string[]
                            {
                                "DROP TABLE IF EXISTS EdsmSystems",
                                "DROP TABLE IF EXISTS SystemNames",
                            });

                            reportProgress?.Invoke("Shrinking database");
                            conn.Connection.Vacuum();

                            reportProgress?.Invoke("Creating indexes");         // NOTE the date should be the same so we don't rewrite
                            conn.Connection.CreateSystemDBTableIndexes();

                            //RebuildRunning = false;
                        }
                        else
                        {
                            conn.Connection.DropStarTables(TempTablePostfix);     // just in case, kill the old tables
                        }
                    });
                }
                else
                {       // newer data is needed, so just remove
                    ExecuteWithDatabase( action: conn =>
                    {
                        reportProgress?.Invoke("Removing old system tables");

                        conn.Connection.ExecuteNonQueries(new string[]
                        {
                            "DROP TABLE IF EXISTS EdsmSystems",
                            "DROP TABLE IF EXISTS SystemNames",
                        });
                    });
                }
            }
        }

        public string GetEDSMGridIDs()
        {
            return Execute(() => SQLiteConnectionSystem.GetSettingString("EDSMGridIDs", "Not Set", Connection.Connection));
        }

        public bool SetEDSMGridIDs(string value)
        {
            return Execute(() => SQLiteConnectionSystem.PutSettingString("EDSMGridIDs", value, Connection.Connection));
        }

        public DateTime GetEDSMGalMapLast()
        {
            return Execute(() => SQLiteConnectionSystem.GetSettingDate("EDSMGalMapLast", DateTime.MinValue, Connection.Connection));
        }

        public bool SetEDSMGalMapLast(DateTime value)
        {
            return Execute(() => SQLiteConnectionSystem.PutSettingDate("EDSMGalMapLast", value, Connection.Connection));
        }

        #region Time markers

        // time markers - keeping the old code for now, not using better datetime funcs

        public void ForceEDSMFullUpdate()
        {
            Execute(() => SQLiteConnectionSystem.PutSettingString("EDSMLastSystems", "2010-01-01 00:00:00", Connection.Connection));
        }

        public DateTime GetLastEDSMRecordTimeUTC()
        {
            return Execute(() =>
            {
                string rwsystime = SQLiteConnectionSystem.GetSettingString("EDSMLastSystems", "2000-01-01 00:00:00", Connection.Connection); // Latest time from RW file.
                DateTime edsmdate;

                if (!DateTime.TryParse(rwsystime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out edsmdate))
                    edsmdate = new DateTime(2000, 1, 1);

                return edsmdate;
            });
        }

        public void SetLastEDSMRecordTimeUTC(DateTime time)
        {
            Execute(() =>
            {
                SQLiteConnectionSystem.PutSettingString("EDSMLastSystems", time.ToString(CultureInfo.InvariantCulture), Connection.Connection);
                System.Diagnostics.Debug.WriteLine("Last EDSM record " + time.ToString());
            });
        }

        public DateTime GetLastEDDBDownloadTime()
        {
            return Execute(() => SQLiteConnectionSystem.GetSettingDate("EDDBLastDownloadTime", DateTime.MinValue, Connection.Connection));
        }

        public void SetLastEDDBDownloadTime()
        {
            Execute(() => SQLiteConnectionSystem.PutSettingDate("EDDBLastDownloadTime", DateTime.UtcNow, Connection.Connection));
        }

        public void ForceEDDBFullUpdate()
        {
            Execute(() => SQLiteConnectionSystem.PutSettingDate("EDDBLastDownloadTime", DateTime.MinValue, Connection.Connection));
        }

        public int GetEDSMSectorIDNext()
        {
            return Execute(() => SQLiteConnectionSystem.GetSettingInt("EDSMSectorIDNext", 1, Connection.Connection));
        }

        public void SetEDSMSectorIDNext(int val)
        {
            Execute(() => SQLiteConnectionSystem.PutSettingInt("EDSMSectorIDNext", val, Connection.Connection));
        }

        #endregion
    }
}
