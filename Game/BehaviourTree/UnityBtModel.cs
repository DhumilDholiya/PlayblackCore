﻿using Fasterflect;
using Playblack.BehaviourTree.Model.Core;
using Playblack.Extensions;
#if UNITY_EDITOR
using Playblack.Sequencer;
#endif
using ProtoBuf;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Playblack.BehaviourTree {

    /// <summary>
    /// Represents a protocontract model of a behaviour tree.
    /// This is usually the data model that gets created in the editor
    /// and is read and managed during runtime.
    /// Now, why add this extra layer?
    /// Because Unity-serialisation itself has only a limited nesting level (of 7).
    /// But behaviour trees may get far more complex and deep than that.
    /// Additionally, it's good to have them compatible with the savegame system.
    /// That's why there is this extra layer of model data.
    /// </summary>
    [ProtoContract]
    public class UnityBtModel {

        [ProtoMember(10)]
        public List<ValueField> contextData;

        [ProtoMember(20, OverwriteList = true)]
        public List<UnityBtModel> children = new List<UnityBtModel>();

        // requirement here is that a class may have a 0-args constructor
        [ProtoMember(30)]
        private string modelClassName;

        public string ModelClassName {
            get {
                return this.modelClassName;
            }
            set {
                this.modelClassName = value;
                // Update context data if empty
                if (this.contextData == null || this.contextData.Count == 0) {
                    this.contextData = new List<ValueField>();
                    this.contextData.AddRange(this.GetProposedFields());
                }
                int proposedNumChildren = this.GetProposedNumChildren();
                if (children == null) {
                    children = new List<UnityBtModel>();
                }
                if ((children.Count == 0 || children.Count < proposedNumChildren) && proposedNumChildren != -1) {
                    this.ResizeChildren(true);
                }
            }
        }

        [ProtoMember(40)]
        private int numChildren = 0;

        [ProtoMember(50)]
        private string displayName;

        /// <summary>
        /// This is set with the ModelClass and defines what name is displayed on the operator selector
        /// and the sequence editor tree.
        /// The value is defined on each relevant modeltask class via the DataDescriptor attribute.
        /// </summary>
        public string DisplayName {
            get {
                return displayName;
            }
        }

        /// <summary>
        /// Used to register certain children within for re-ordering inside the child-list.
        /// This is required in order to avoid concurrent modifications to the child list.
        /// </summary>
        private Dictionary<UnityBtModel, int> scheduledReorders = new Dictionary<UnityBtModel, int>();

        private ModelTask mtInstance;

        public ModelTask Model {
            get {
                if (mtInstance != null) {
                    mtInstance.Context = new DataContext(this.contextData);
                    return mtInstance;
                }
                // First: introspect the class to see if it's one we can work with
                var type = this.ModelType;
                //if (!type.IsAssignableFrom(typeof(ModelTask))) {
                if (!typeof(ModelTask).IsAssignableFrom(type)) {
                    throw new ArgumentException("'" + type + "' is not a ModelTask object. I don't know how to use it. Fix it.");
                }
                var ctor = type.GetConstructor(Type.EmptyTypes);
                // guards and stuff must be set in the method that reads the tree.
                var mt = (ModelTask)ctor.Invoke(new object[] { });
                if (this.contextData != null) {
                    mt.Context = new DataContext(this.contextData);
                }
                this.mtInstance = mt;
                return mt;
            }
        }

        public Type ModelType {
            get {
                var type = Type.GetType(this.modelClassName);
                if (type != null) {
                    return type;
                }
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies()) {
                    type = a.GetType(this.modelClassName);
                    if (type != null) {
                        return type;
                    }
                }
                return null;
            }
        }

#if UNITY_EDITOR
        private string cachedCodeViewDisplay;
        private Dictionary<int, string> childDisplayCache = new Dictionary<int, string>();

        public string CodeViewDisplay {
            get {
                if (cachedCodeViewDisplay == null) {
                    UpdateCodeViewDisplay();
                }
                return cachedCodeViewDisplay;
            }
        }

        public void UpdateCodeViewDisplay() {
            if (contextData == null || contextData.Count == 0) {
                cachedCodeViewDisplay = this.DisplayName;
                return;
            }
            var t = ModelType;
            if (t == null) {
                // There's nothing yet, use displayname if any
                // otherwise stuff is just null.
                cachedCodeViewDisplay = this.DisplayName;
                return;
            }
            var attrib = t.Attribute<CodeViewFormattingHintAttribute>();
            if (attrib == null) {
                cachedCodeViewDisplay = this.DisplayName;
                return;
            }

            var theString = attrib.Format;
            theString = ReplacePlaceholdersWithContextData(theString);
            cachedCodeViewDisplay = theString;
            this.childDisplayCache.Clear();
        }

        private string ReplacePlaceholdersWithContextData(string theString) {
            foreach (var valueField in this.contextData) {
                string valueString = valueField.UnityValue;
                if (string.IsNullOrEmpty(valueString)) {
                    continue;
                }
                if (!string.IsNullOrEmpty(valueString) && valueString.Length > 50) {
                    valueString = valueString.Substring(0, 47) + "...";
                }
                theString = theString.Replace("{" + valueField.Name + "}", valueString);
            }

            return theString;
        }

        public string GetChildDisplayName(int insertIndex) {
            if (childDisplayCache.ContainsKey(insertIndex)) {
                return childDisplayCache[insertIndex];
            }
            var childs = GetChildStructure();
            bool foundInsertIndex = false;
            for (int i = 0; i < childs.Count; ++i) {
                if (childs[i].InsertIndex == insertIndex) {
                    var theString = ReplacePlaceholdersWithContextData(childs[i].Name);
                    this.childDisplayCache.Add(insertIndex, theString);
                    foundInsertIndex = true;
                    break;
                }
            }
            if (!foundInsertIndex) {
                if (childs.Count < insertIndex && childs.Count > 0) {
                    childDisplayCache.Add(insertIndex, childs[insertIndex].Name);
                }
                else {
                    return "No child descriptor on insert index " + insertIndex;
                }
            }
            return childDisplayCache[insertIndex];
        }

#endif

        public static UnityBtModel NewInstance(UnityBtModel parent) {
            var model = new UnityBtModel();
            model.children = new List<UnityBtModel>();
            if (parent != null) {
                parent.children.Add(model);
            }
            return model;
        }

        public static UnityBtModel NewInstance(UnityBtModel parent, UnityBtModel model, string modelClassName) {
            if (model != null) { // We can explicitly set null values you see
                model.children = new List<UnityBtModel>();
                model.contextData = new List<ValueField>();
                model.ModelClassName = modelClassName;
            }
            if (parent != null) {
                parent.children.Add(model);
            }

            return model;
        }

        public static UnityBtModel NewInstance(UnityBtModel parent, UnityBtModel model, string modelClassName, int insertIndex) {
            if (model != null) { // We can explicitly set null values you see
                model.children = new List<UnityBtModel>();
                model.contextData = new List<ValueField>();
                model.ModelClassName = modelClassName;
            }
            if (parent != null) {
                // NOTE: This means the children list must be populated already, at least with nulls!
                if (insertIndex >= parent.children.Count) {
                    parent.children.Resize(insertIndex + 1, null);
                }
                parent.children[insertIndex] = model;
            }

            return model;
        }

        public bool RemoveChild(UnityBtModel model) {
            int index = this.children.IndexOf(model);
            if (index >= 0) {
                var child = children[index];
                children.RemoveAt(index);
                // child.ResizeChildren(); // why do this?
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets a child at the given index to null
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public bool NullChild(UnityBtModel model) {
            int index = children.IndexOf(model);
            if (index == -1) {
                return false;
            }
            Debug.Log("Removing child at index " + index);
            children[index] = null;
            return true;
        }

        public void ResizeChildren() {
            ResizeChildren(false);
        }

        /// <summary>
        /// Resize the children array to the currently set numChildren
        /// </summary>
        public void ResizeChildren(bool useNulls) {
            bool needsRefill = children.Count < numChildren;

            if (needsRefill) {
                var diff = numChildren - children.Count;
                for (int i = 0; i < diff; ++i) {
                    if (useNulls) {
                        this.children.Add(null);
                    }
                    else {
                        UnityBtModel.NewInstance(this);
                    }
                }
            }
            else {
                // Shrinking
                while (children.Count > numChildren) {
                    var child = children[children.Count - 1];
                    children.RemoveAt(children.Count - 1);
                    // Make sure the now inaccessible children get pruged properly
                    child.ResizeChildren(useNulls);
                    child = null;
                }
            }
            // just to be sure
            numChildren = children.Count;
        }

        public void ScheduleChildReorder(UnityBtModel child, int newIndex) {
            scheduledReorders.Add(child, newIndex);
        }

        public void ProcessReorders() {
            foreach (var kvp in scheduledReorders) {
                if (kvp.Value >= this.children.Count || kvp.Value < 0) {
                    // This means we're either at the beginning or at the end
                    Debug.Log("Hit one end of a list, not reordering this child. New Index is told to be " + kvp.Value);
                    continue;
                }
                int i = this.children.IndexOf(kvp.Key);
                this.children.RemoveAt(i);
                this.children.Insert(kvp.Value, kvp.Key);
            }
            scheduledReorders.Clear();
        }

        /// <summary>
        /// Get an array with proposed fields for the underlying Model.
        /// These are used for setting defaults in the Sequence editor windows.
        /// This is a list of fields annotated with the EditableFieldAttribute.
        /// If there is no such attribute on any field, an empty array is returned.
        /// </summary>
        /// <returns>The proposed fields.</returns>
        private ValueField[] GetProposedFields() {
            var dataDescriptor = ModelType.Attribute<ModelDataDescriptorAttribute>();
            this.displayName = dataDescriptor.OperatorName;
            if (dataDescriptor.DataContextDescription != null && dataDescriptor.DataContextDescription.Count > 0) {
                var proposedFields = dataDescriptor.DataContextDescription;
                var valueFields = new ValueField[proposedFields.Count];
                int i = 0;
                foreach (var kvp in proposedFields) {
                    var newValueField = new ValueField();
                    newValueField.Type = kvp.Value.FieldValueType;
                    // Since this is used to map back to the actual field on the executor class, it must be unique.
                    // Uniqueness is enfored in the datadescriptor constructor.
                    newValueField.Name = kvp.Value.DisplayName;

                    if (kvp.Value.DefaultUnityValue != null) {
                        newValueField.Value = kvp.Value.DefaultUnityValue;
                    }
                    valueFields[i++] = newValueField;
                }
                return valueFields;
            }
            return new ValueField[0];
        }

        // Counter-act the fact that empty lists deserialize as null
        [ProtoAfterDeserialization]
        private void OnDeserialize() {
            if (this.children == null) {
                this.children = new List<UnityBtModel>();
            }
            if (this.contextData == null) {
                this.contextData = new List<ValueField>();
            }
        }

        /// <summary>
        /// Returns the proposed number of children.
        /// Negative values indicate an undefined amount of children.
        /// </summary>
        /// <returns></returns>
        public int GetProposedNumChildren() {
            return ModelType.Attribute<ModelDataDescriptorAttribute>().NumChildren;
        }

#if UNITY_EDITOR
        private List<ChildDescriptorAttribute> childStructure;

        public IList<ChildDescriptorAttribute> GetChildStructure() {
            if (childStructure != null) {
                return childStructure;
            }
            if (this.ModelClassName == null) {
                // NOTE: So far this seems to be fixed.
                // I'll keep the condition ust in case (20161120)
                Debug.LogError("No classname specified ... Much error!");
                return new List<ChildDescriptorAttribute>(0);
            }
            this.childStructure = new List<ChildDescriptorAttribute>();
            // unity doesn't want the type argument version so we gotta do this instead ...
            var childDescriptors = ModelType.Attributes(typeof(ChildDescriptorAttribute));
            for (int i = 0; i < childDescriptors.Count; ++i) {
                this.childStructure.Add((ChildDescriptorAttribute)childDescriptors[i]);
                
            }

            this.childStructure.Sort((a, b) => {
                if (a.DisplayDelta > b.DisplayDelta) {
                    return -1;
                }
                else if (a.DisplayDelta < b.DisplayDelta) {
                    return 1;
                }
                else {
                    return 0;
                }
            });
            return this.childStructure;
        }

#endif
    }
}