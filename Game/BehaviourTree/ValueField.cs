﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;
using UnityEngine;

namespace Playblack.BehaviourTree {
    /**
    * Loosely typed serializable value field thing for setting arbitrary data in unity editor.
    * Is used to assign values to anything that isn't ConVars
    */
    [Serializable]
    [ProtoContract]
    public class ValueField {

        [SerializeField]
        [ProtoMember(1)]
        private string name;

        public string Name {
            get {
                return this.name;
            }
            set {
                this.name = value;
            }
        }

#if UNITY_EDITOR
        [SerializeField]
        [ProtoMember(2)]
        public string unityValue;
#else
        [SerializeField]
        [ProtoMember(2)]
        private string unityValue;
        public string UnityValue {
            get {
                return unityValue;
            }
        }
#endif

        public object Value {
            get {
                if (unityValue != null) {
                    switch (varType) {
                        case ValueType.BOOL:
                            return bool.Parse(unityValue);
                        case ValueType.FLOAT:
                            return float.Parse(unityValue);
                        case ValueType.INT:
                            return int.Parse(unityValue);
                        case ValueType.STRING:
                            return unityValue;
                    }
                }
                return null;
            }
            set {
                if (value != null) {
                    this.unityValue = value.ToString();
                }
                else {
                    this.unityValue = null;
                }
            }
        }

        [SerializeField]
        [ProtoMember(3)]
        private ValueType varType;

        public ValueType Type {
            get {
                return varType;
            }
            set {
                varType = value;
            }
        }

        public ValueField() {

        }

        public ValueField(string name, string value, ValueType type) {
            this.Name = name;
            this.unityValue = value;
            this.Type = type;
        }
    }
}
