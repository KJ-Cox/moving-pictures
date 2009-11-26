﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using NLog;
using Cornerstone.GUI.Dialogs;
using Cornerstone.Database.Tables;
using Cornerstone.Database;
using System.Reflection;

namespace MediaPortal.Plugins.MovingPictures.Database {
    public class DatabaseMaintenanceManager {

        private static Logger logger = LogManager.GetCurrentClassLogger();
        
        public static event ProgressDelegate MaintenanceProgress;

        // Loops through all local files in the system and removes anything that's invalid.
        public static void RemoveInvalidFiles() {
           
            logger.Info("Checking for invalid file entries in the database.");

            float count = 0;
            List<DBLocalMedia> files = DBLocalMedia.GetAll();
            float total = files.Count;
            
            int cleaned = 0;
            
            foreach (DBLocalMedia currFile in files) {
                if (MaintenanceProgress != null) MaintenanceProgress("", (int)(count * 100 / total));
                count++;

                // Skip previously deleted files
                if (currFile.ID == null)
                    continue;

                // remove files without an import path
                if (currFile.ImportPath == null || currFile.ImportPath.ID == null) {
                    logger.Info("Removing: {0} (no import path)", currFile.FullPath);
                    currFile.Delete();
                    cleaned++;
                    continue;
                }

                // Remove Orphan Files
                if (currFile.AttachedMovies.Count == 0 && !currFile.Ignored) {
                    logger.Info("Removing: {0} (orphan)", currFile.FullPath);
                    currFile.Delete();
                    cleaned++;
                    continue;
                }

                // Remove entries from the database that have their file removed
                if (currFile.IsRemoved) {
                    logger.Info("Removing: {0} (file is removed)", currFile.FullPath);
                    currFile.Delete();
                    cleaned++;
                }

            }
            
            logger.Info("Removed {0} file entries.", cleaned.ToString());
            if (MaintenanceProgress != null) MaintenanceProgress("", 100);
        }
        
        // Loops through all movie in the system to verify them
        public static void VerifyMovieInformation() {
            logger.Info("Updating Movie Information...");

            float count = 0;
            List<DBMovieInfo> movies = DBMovieInfo.GetAll();
            List<DBUser> users = DBUser.GetAll();
            float total = movies.Count;

            int removed = 0;
            int settings = 0;
            foreach (DBMovieInfo movie in movies) {
                if (MaintenanceProgress != null) MaintenanceProgress("", (int)(count * 100 / total));
                count++;

                // Skip uncommited files
                if (movie.ID == null)
                    continue;

                #region Remove movie without attached local media

                // Remove movie with no files
                if (movie.LocalMedia.Count == 0) {
                    logger.Info("'{0}' was removed from the system because it had no local media.", movie.Title);
                    movie.Delete();
                    removed++;
                    continue;
                }

                #endregion

                #region Add missing user settings

                if (movie.UserSettings.Count == 0) {
                    logger.Info("'{0}' was missing UserMovingSettings, adding now.", movie.Title);
                    foreach (DBUser currUser in users) {
                        DBUserMovieSettings userSettings = new DBUserMovieSettings();
                        userSettings.User = currUser;
                        userSettings.Commit();
                        movie.UserSettings.Add(userSettings);
                        userSettings.CommitNeeded = false;
                    }
                    movie.Commit();
                    settings++;
                }

                #endregion

            }

            logger.Info("Removed {0} movie entries.", removed.ToString());
            logger.Info("Updated {0} movie entries with default user setting.", settings.ToString());
            if (MaintenanceProgress != null) MaintenanceProgress("", 100);
        }

        // Update System Managed Import Paths
        public static void UpdateImportPaths() {
            
            // remove invalid import paths
            foreach (DBImportPath currPath in DBImportPath.GetAll()) {
                if (currPath.Directory == null)
                    currPath.Delete();

                if (currPath.InternallyManaged && currPath.GetDriveType() == DriveType.NoRootDirectory) {
                    currPath.Delete();
                    logger.Info("Removed system managed import path: {0} (drive does not exist)", currPath.FullPath);
                }

            }

            float count = 0;
            float total = DriveInfo.GetDrives().Length; 

            bool daemonEnabled = MovingPicturesCore.MediaPortalSettings.GetValueAsBool("daemon", "enabled", false);
            string virtualDrive = MovingPicturesCore.MediaPortalSettings.GetValueAsString("daemon", "drive", "?:");
            
            // Get all drives
            foreach (DriveInfo drive in DriveInfo.GetDrives()) {
                if (MaintenanceProgress != null) MaintenanceProgress("", (int)(count * 100 / total));
                count++;

                // Add the import path if it does not exist and 
                // is not marked virtual by MediaPortal.
                DBImportPath importPath = DBImportPath.Get(drive.Name);
                bool isVirtual = drive.Name.StartsWith(virtualDrive, StringComparison.OrdinalIgnoreCase) && daemonEnabled;
                bool isCDRom = (drive.DriveType == DriveType.CDRom);

                if (importPath.ID != null) {
                    // Remove an system managed path if for any reason it's not of type CDRom
                    if (!isCDRom && importPath.InternallyManaged) {
                        importPath.Delete();
                        logger.Info("Removed system managed import path: {0} (drive type has changed)", importPath.FullPath);
                        continue;
                    }

                    // Remove an existing path if it's defined as the virtual drive
                    if (isVirtual) {
                        importPath.Delete();
                        logger.Info("Removed import path: {0} (drive is marked as virtual)", importPath.FullPath);
                        continue;
                    }

                    // Update an existing import path to a system managed import path
                    // if the drive type is CDRom but the system flag isn't set
                    if (isCDRom && !importPath.InternallyManaged) {
                        importPath.InternallyManaged = true;
                        importPath.Commit();
                        logger.Info("{0} was updated to a system managed import path.", importPath.FullPath);
                    }

                }
                else {
                    if (isCDRom && !isVirtual) {
                        importPath.InternallyManaged = true;
                        importPath.Commit();
                        logger.Info("Added system managed import path: {0}", importPath.FullPath);
                    }
                }
            }

            if (MaintenanceProgress != null) MaintenanceProgress("", 100);
        }
        
        // One time upgrade tasks for movie information
        public static void PerformMovieInformationUpgradeCheck() {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;
            logger.Info("Performing Movie Information Upgrade Check...");

            float count = 0;
            List<DBMovieInfo> movies = DBMovieInfo.GetAll();
            float total = movies.Count;

            foreach (DBMovieInfo movie in movies) {
                if (MaintenanceProgress != null) MaintenanceProgress("", (int)(count * 100 / total));
                count++;

                // Skip uncommited files
                if (movie.ID == null)
                    continue;

                #region Upgrades required for 0.7.1

                if (MovingPicturesCore.GetDBVersionNumber() < new Version("0.7.1")) {

                    if (movie.LocalMedia.Count > 0 && movie.LocalMedia[0].ImportPath != null) {
                        if (movie.LocalMedia[0].ImportPath.IsOpticalDrive && movie.LocalMedia[0].IsAvailable) {
                            movie.DateAdded = movie.LocalMedia[0].File.CreationTime;
                        }
                        else {
                            movie.DateAdded = movie.DateAdded.AddSeconds((double)movie.ID);
                        }
                    }

                    
                }

                #endregion

                // commit movie
                movie.Commit();
            }

            if (MaintenanceProgress != null) MaintenanceProgress("", 100);
        }

        // One time upgrades tasks for file information
        public static void PerformFileInformationUpgradeCheck() {
            Version ver = Assembly.GetExecutingAssembly().GetName().Version;
            logger.Info("Performing File Information Upgrade Check...");

            float count = 0;
            List<DBLocalMedia> files = DBLocalMedia.GetAll();
            float total = files.Count;

            foreach (DBLocalMedia currFile in files) {
                if (MaintenanceProgress != null) MaintenanceProgress("", (int)(count * 100 / total));
                count++;

                // Skip uncommited files
                if (currFile.ID == null)
                    continue;

                #region Upgrades required for 0.8.0

                if (MovingPicturesCore.GetDBVersionNumber() < new Version("0.8.0")) {
                    
                    // Disk Information Upgrade
                    if (!currFile.ImportPath.IsOpticalDrive && currFile.ImportPath.IsAvailable) {
                        // Skip optical drives, unc paths and unavailable files
                        if (String.IsNullOrEmpty(currFile.VolumeSerial)) {
                            // perform update
                            currFile.UpdateVolumeInformation();
                            logger.Info("Disk information updated for '{0}'", currFile.FullPath);
                        }
                    }

                }

                #endregion

                // commit file
                currFile.Commit();
            }

            if (MaintenanceProgress != null) MaintenanceProgress("", 100);
        }

        public static void VerifyFilterMenu() {
            DBMenu<DBMovieInfo> menu = MovingPicturesCore.Settings.FilterMenu;

            if (menu.RootNodes.Count == 0) {
                int position = 1;

                DBNode<DBMovieInfo> unwatchedNode = new DBNode<DBMovieInfo>();
                unwatchedNode.Name = "Unwatched Movies";
                unwatchedNode.DynamicNode = false;
                unwatchedNode.Filter = new DBFilter<DBMovieInfo>();
                DBCriteria<DBMovieInfo> criteria = new DBCriteria<DBMovieInfo>();
                criteria.Field = DBField.GetFieldByDBName(typeof(DBUserMovieSettings), "watched");
                criteria.Relation = DBRelation.GetRelation(typeof(DBMovieInfo), typeof(DBUserMovieSettings), "");
                criteria.Operator = DBCriteria<DBMovieInfo>.OperatorEnum.EQUAL;
                criteria.Value = "0";
                unwatchedNode.Filter.Criteria.Add(criteria);
                unwatchedNode.SortPosition = position++;
                unwatchedNode.DBManager = MovingPicturesCore.DatabaseManager;
                menu.RootNodes.Add(unwatchedNode);


                DBNode<DBMovieInfo> genreNode = new DBNode<DBMovieInfo>();
                genreNode.DynamicNode = true;
                genreNode.BasicFilteringField = DBField.GetFieldByDBName(typeof(DBMovieInfo), "genres");
                genreNode.Name = "Genre";
                genreNode.DBManager = MovingPicturesCore.DatabaseManager;
                genreNode.SortPosition = position++;
                menu.RootNodes.Add(genreNode);

                DBNode<DBMovieInfo> yearNode = new DBNode<DBMovieInfo>();
                yearNode.DynamicNode = true;
                yearNode.BasicFilteringField = DBField.GetFieldByDBName(typeof(DBMovieInfo), "year");
                yearNode.Name = yearNode.BasicFilteringField.Name;
                yearNode.DBManager = MovingPicturesCore.DatabaseManager;
                yearNode.SortPosition = position++;
                menu.RootNodes.Add(yearNode);

                DBNode<DBMovieInfo> certNode = new DBNode<DBMovieInfo>();
                certNode.DynamicNode = true;                                       
                certNode.BasicFilteringField = DBField.GetFieldByDBName(typeof(DBMovieInfo), "certification");
                certNode.Name = certNode.BasicFilteringField.Name;
                certNode.DBManager = MovingPicturesCore.DatabaseManager;
                certNode.SortPosition = position++;
                menu.RootNodes.Add(certNode);

                DBNode<DBMovieInfo> dateNode = new DBNode<DBMovieInfo>();
                dateNode.DynamicNode = true;
                dateNode.BasicFilteringField = DBField.GetFieldByDBName(typeof(DBMovieInfo), "date_added");
                dateNode.Name = "Date Added";
                dateNode.DBManager = MovingPicturesCore.DatabaseManager;
                dateNode.SortPosition = position++;
                menu.RootNodes.Add(dateNode);

                menu.Commit();
            }

            foreach (DBNode<DBMovieInfo> currNode in menu.RootNodes) {
                currNode.UpdateDynamicNode();
                currNode.Commit();
            }
        }

        public static void VerifyCategoryMenu() {
            DBMenu<DBMovieInfo> menu = MovingPicturesCore.Settings.CategoriesMenu;

            if (menu.RootNodes.Count == 0) {
                int position = 1;

                DBNode<DBMovieInfo> allNode = new DBNode<DBMovieInfo>();
                allNode.Name = "${AllMovies}";
                allNode.DynamicNode = false;
                allNode.Filter = new DBFilter<DBMovieInfo>();
                allNode.SortPosition = position++;
                allNode.DBManager = MovingPicturesCore.DatabaseManager;
                menu.RootNodes.Add(allNode);

                DBNode<DBMovieInfo> unwatchedNode = new DBNode<DBMovieInfo>();
                unwatchedNode.Name = "${UnwatchedMovies}";
                unwatchedNode.DynamicNode = false;
                unwatchedNode.Filter = new DBFilter<DBMovieInfo>();
                DBCriteria<DBMovieInfo> criteria = new DBCriteria<DBMovieInfo>();
                criteria.Field = DBField.GetFieldByDBName(typeof(DBUserMovieSettings), "watched");
                criteria.Relation = DBRelation.GetRelation(typeof(DBMovieInfo), typeof(DBUserMovieSettings), "");
                criteria.Operator = DBCriteria<DBMovieInfo>.OperatorEnum.EQUAL;
                criteria.Value = "0";
                unwatchedNode.Filter.Criteria.Add(criteria);
                unwatchedNode.SortPosition = position++;
                unwatchedNode.DBManager = MovingPicturesCore.DatabaseManager;
                menu.RootNodes.Add(unwatchedNode);

                DBNode<DBMovieInfo> recentNode = new DBNode<DBMovieInfo>();
                recentNode.Name = "${RecentlyAddedMovies}";
                recentNode.DynamicNode = false;
                recentNode.Filter = new DBFilter<DBMovieInfo>();
                DBCriteria<DBMovieInfo> recentCriteria = new DBCriteria<DBMovieInfo>();
                recentCriteria.Field = DBField.GetFieldByDBName(typeof(DBMovieInfo), "date_added");
                recentCriteria.Operator = DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN;
                recentCriteria.Value = "-30d";
                recentNode.Filter.Criteria.Add(recentCriteria);
                recentNode.SortPosition = position++;
                recentNode.DBManager = MovingPicturesCore.DatabaseManager;
                menu.RootNodes.Add(recentNode);

                DBNode<DBMovieInfo> genreNode = new DBNode<DBMovieInfo>();
                genreNode.DynamicNode = true;
                genreNode.BasicFilteringField = DBField.GetFieldByDBName(typeof(DBMovieInfo), "genres");
                genreNode.Name = "${Genre}";
                genreNode.SortPosition = position++;
                genreNode.DBManager = MovingPicturesCore.DatabaseManager;
                menu.RootNodes.Add(genreNode);

                DBNode<DBMovieInfo> certNode = new DBNode<DBMovieInfo>();
                certNode.DynamicNode = true;
                certNode.BasicFilteringField = DBField.GetFieldByDBName(typeof(DBMovieInfo), "certification");
                certNode.Name = "${" + certNode.BasicFilteringField.Name + "}";
                certNode.DBManager = MovingPicturesCore.DatabaseManager;
                certNode.SortPosition = position++;
                menu.RootNodes.Add(certNode);

                DBNode<DBMovieInfo> yearNode = new DBNode<DBMovieInfo>();
                yearNode.DynamicNode = true;
                yearNode.BasicFilteringField = DBField.GetFieldByDBName(typeof(DBMovieInfo), "year");
                yearNode.Name = "${" + yearNode.BasicFilteringField.Name + "}";
                yearNode.SortPosition = position++;
                yearNode.DBManager = MovingPicturesCore.DatabaseManager;
                menu.RootNodes.Add(yearNode);

                menu.Commit();
            }

            foreach (DBNode<DBMovieInfo> currNode in menu.RootNodes) {
                currNode.UpdateDynamicNode();
                currNode.Commit();
            }
        }
    }
}
