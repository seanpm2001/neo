using System;
using System.Collections;
using System.Data;
using Neo.Core;


namespace Neo.Framework
{
	/// <summary>
	/// This is to be subclassed to provide strongly typed representations of the set
	/// of objects which are part of a relation.
	/// </summary>
	public abstract class ObjectRelationBase : ObjectCollectionBase
	{
		//--------------------------------------------------------------------------------------
		//	Fields and constructor
		//--------------------------------------------------------------------------------------

		internal readonly IEntityObject Owner;
		internal readonly DataRelation Relation;
		protected		  IList innerList;

		protected ObjectRelationBase(IEntityObject eo, DataRelation aRelation)
		{
			if(aRelation.ParentColumns.Length > 1)
				throw new ArgumentException("DataRelation cannot have compound keys.");
			Owner = eo;
			Relation = aRelation;
		}


		//--------------------------------------------------------------------------------------
		//	Protected properties
		//--------------------------------------------------------------------------------------

		protected override IList InnerList
		{
			get
			{
				if(innerList == null)
					Load();
				return innerList;
			}
		}


		//--------------------------------------------------------------------------------------
		//	Some properties to make the code below more readable
		//--------------------------------------------------------------------------------------

		private string foreignTableName
		{
			get { return Relation.ChildTable.TableName; }
		}

		private string localColumnName
		{
			get { return Relation.ParentColumns[0].ColumnName; }
		}

		private string foreignColumnName
		{
			get { return Relation.ChildColumns[0].ColumnName; }
		}


		//--------------------------------------------------------------------------------------
		//	Collection management
		//--------------------------------------------------------------------------------------

		protected virtual void Load()
		{
			innerList = Owner.Context.GetObjectsFromTable(foreignTableName, foreignColumnName, Owner.Row[localColumnName]);

			Relation.ChildTable.ColumnChanging += new DataColumnChangeEventHandler(this.ForeignColumnChanging);
			Relation.ChildTable.RowDeleting += new DataRowChangeEventHandler(this.ForeignRowEvent);
		}

		private object objectForForeignRow(DataRow row, bool tryDeleted)
		{
			IEntityMap emap = Owner.Context.EntityMapFactory.GetMap(row.Table.TableName);
			object[] pkvalues = Owner.Context.GetPrimaryKeyValuesForRow(emap, row, DataRowVersion.Current);
			object eo = Owner.Context.ObjectTable.GetObject(foreignTableName, pkvalues);
			if((eo == null) && tryDeleted)
				eo = Owner.Context.ObjectTable.GetDeletedObject(foreignTableName, pkvalues);
			return eo;
		}

		protected virtual void ForeignColumnChanging(object sender, DataColumnChangeEventArgs e)
		{
			if(e.Column.ColumnName != foreignColumnName)
				return;

			// Check whether our owner is deleted. In this case, of course, we don't
			// need to do anything. (Actually, we can't...)
			if((Owner.Row.RowState == DataRowState.Deleted) || (Owner.Row.RowState == DataRowState.Detached))
				return;

			// Early exits for cases where the FK is also the PK; most likely with
			// a compound key in correlation tables. The PK is set, this event 
			// fired, but the object is not known to the context yet. We just ignore
			// the event and rely on the context resending the event later.
			if(e.Row == Owner.Context.RowPending) 
				return; 

			object newValue = e.ProposedValue;
			object oldValue = e.Row[foreignColumnName];
			if((newValue != null) && (newValue.Equals(Owner.Row[localColumnName])))
			{	
				object eo = objectForForeignRow(e.Row, false);
				// We add the object when (a) we get a change event or (b) we get
				// a 'no-change' but the object is not in our list; and is not null.
				if((newValue.Equals(oldValue) == false) || ((eo != null) && (innerList.Contains(eo) == false)))
					innerList.Add(eo);
			}
			if((oldValue != null) && (oldValue.Equals(Owner.Row[localColumnName])))
			{
				object eo = objectForForeignRow(e.Row, false);
                // We remove the object when we get a change event
				if(oldValue.Equals(newValue) == false)
					innerList.Remove(eo);
			}
		}

		protected virtual void ForeignRowEvent(object sender, DataRowChangeEventArgs e)
		{
			if(e.Action != DataRowAction.Delete)
				return;

			// Check whether our owner is deleted. In this case, of course, we don't
			// need to do anything. (Actually, we can't...)
			if((Owner.Row.RowState == DataRowState.Deleted) || (Owner.Row.RowState == DataRowState.Detached))
				return;

			// Normally, to read the PK we have to access the original version but
			// if the child row which is being deleted was new (i.e. just added), we
			// must not access the original version (which it doesn't have because it 
			// is new.) but the current version...
			DataRowVersion lookupVersion = DataRowVersion.Original;
			if(e.Row.RowState == DataRowState.Added)
				lookupVersion = DataRowVersion.Current;

			// If object refers to our owner, remove it from the list.
			if(e.Row[foreignColumnName, lookupVersion].Equals(Owner.Row[localColumnName]))
				innerList.Remove(objectForForeignRow(e.Row, true));
		}

		
		//--------------------------------------------------------------------------------------
		//	Untyped implementations to be called by subclasses
		//--------------------------------------------------------------------------------------

		protected override void Insert(int index, IEntityObject eo)
		{
			throw new InvalidOperationException("Cannot insert into an ObjectRelation. Must use Add instead.");
		}

		protected override int Add(IEntityObject newObject)
		{
			newObject.Row[foreignColumnName] = Owner.Row[localColumnName];
			return IndexOf(newObject);
		}

		protected override void Remove(IEntityObject existingObject)
		{
			existingObject.Row[foreignColumnName] = DBNull.Value;
		}

		protected virtual void CopyToListAndMakeReadOnly(ObjectListBase list)
		{
			foreach(object o in this)
				((IList)list).Add(o);
			list.MakeReadOnly();
		}

		protected virtual void CopyToListAndSort(ObjectListBase list, string propName, SortDirection dir)
		{
			foreach(object o in this)
				((IList)list).Add(o);
			list.Sort(propName, dir);
			list.MakeReadOnly();
		}


		//--------------------------------------------------------------------------------------
		//	Public properties and methods
		//--------------------------------------------------------------------------------------

		public virtual void Touch()
		{
			if(innerList == null)
				Load();
		}

	    public virtual void InvalidateCache()
		{
			innerList = null;

			Relation.ChildTable.ColumnChanging -= new DataColumnChangeEventHandler(this.ForeignColumnChanging);
			Relation.ChildTable.RowDeleting -= new DataRowChangeEventHandler(this.ForeignRowEvent);
		}

	}
}
