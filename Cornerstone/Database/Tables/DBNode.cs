﻿using System;
using System.Collections.Generic;
using System.Text;
using Cornerstone.Database.CustomTypes;

namespace Cornerstone.Database.Tables {
    [DBTable("node")]
    public class DBNode<T>: DatabaseTable where T: DatabaseTable {
        #region Database Fields

        [DBField]
        public string Name {
            get { return _name; }
            
            set {
                _name = value;
                commitNeeded = true;
            }
        }
        private string _name;

        [DBField]
        public DBField BasicFilteringField {
            get { return _basicFilteringField; }

            set {
                _basicFilteringField = value;
                commitNeeded = true;
            }
        } 
        private DBField _basicFilteringField = null;

        [DBField]
        public DBRelation BasicFilteringRelation {
            get { return _basicFilteringRelation; }

            set {
                _basicFilteringRelation = value;
                commitNeeded = true;
            }
        } 
        private DBRelation _basicFilteringRelation;

        [DBField]
        public bool AutoGenerated {
            get { return _autoGenerated; }
            
            set {
                _autoGenerated = value;
                commitNeeded = true;
            }
        } 
        private bool _autoGenerated = false;

        [DBRelation(AutoRetrieve = true)]
        public RelationList<DBNode<T>, DBFilter<T>> Filters {
            get {
                if (_filters == null) {
                    _filters = new RelationList<DBNode<T>, DBFilter<T>>(this);
                }
                return _filters;
            }
        } 
        RelationList<DBNode<T>, DBFilter<T>> _filters;

        [DBRelation(AutoRetrieve = true, OneWay=true)]
        public RelationList<DBNode<T>, DBNode<T>> Children {
            get {
                if (_children == null) {
                    _children = new RelationList<DBNode<T>, DBNode<T>>(this);
                }
                return _children;
            }
        } 
        RelationList<DBNode<T>, DBNode<T>> _children;

        #endregion

        public override void AfterCommit() {
            foreach (DBNode<T> currNode in Children)
                DBManager.Commit(currNode);
        }
    }
}