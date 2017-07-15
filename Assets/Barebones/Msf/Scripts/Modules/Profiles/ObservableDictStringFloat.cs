﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Barebones.Networking;

namespace Barebones.MasterServer
{
    public class ObservableDictStringFloat : ObservableBase
    {
        private const int SetOperation = 0;
        private const int RemoveOperation = 1;

        private Dictionary<string, float> _values;

        private Queue<UpdateEntry> _updates;

        public ObservableDictStringFloat(short key) : this(key, null)
        {
        }

        public ObservableDictStringFloat(short key, Dictionary<string, float> defaultValues) : base(key)
        {
            _updates = new Queue<UpdateEntry>();

            _values = defaultValues == null ? new Dictionary<string, float>() :
                defaultValues.ToDictionary(k => k.Key, k => k.Value);
        }

        /// <summary>
        /// Returns an immutable list of values
        /// </summary>
        public IEnumerable<float> Values
        {
            get { return _values.Values; }
        }

        /// <summary>
        /// Returns an immutable list of key-value pairs
        /// </summary>
        public IEnumerable<KeyValuePair<string, float>> Pairs
        {
            get { return _values.ToList(); }
        }

        /// <summary>
        /// Returns a mutable dictionary
        /// </summary>
        public Dictionary<string, float> UnderlyingDictionary
        {
            get { return _values; }
        }

        public void SetValue(string key, float value)
        {
            if (_values.ContainsKey(key))
            {
                _values[key] = value;
            }
            else
            {
                _values.Add(key, value);
            }

            MarkDirty();
            _updates.Enqueue(new UpdateEntry()
            {
                Key = key,
                Operation = SetOperation,
                Value = value
            });
        }

        public void Remove(string key)
        {
            _values.Remove(key);

            MarkDirty();
            _updates.Enqueue(new UpdateEntry()
            {
                Key = key,
                Operation = RemoveOperation,
            });
        }

        public float GetValue(string key)
        {
            float result;
            _values.TryGetValue(key, out result);
            return result;
        }

        public override byte[] ToBytes()
        {
            return _values.ToBytes();
        }

        public override void FromBytes(byte[] data)
        {
            _values.FromBytes(data);
        }

        public override string SerializeToString()
        {
            var obj = new JSONObject();

            foreach (var pair in _values)
            {
                obj.AddField(pair.Key, pair.Value);
            }

            return obj.ToString();
        }

        public override void DeserializeFromString(string value)
        {
            var parsed = new JSONObject(value);
            var keys = parsed.keys;
            var list = parsed.list;

            if (keys == null)
                return;

            for (var i = 0; i < keys.Count; i++)
            {
                _values[keys[i]] = list[i].f;
            }
        }

        public override byte[] GetUpdates()
        {
            using (var ms = new MemoryStream())
            {
                using (var writer = new EndianBinaryWriter(EndianBitConverter.Big, ms))
                {
                    writer.Write(_updates.Count);

                    foreach (var update in _updates)
                    {
                        writer.Write(update.Operation);
                        writer.Write(update.Key);

                        if (update.Operation != RemoveOperation)
                        {
                            writer.Write(update.Value);
                        }
                    }
                }
                return ms.ToArray();
            }
        }

        public override void ApplyUpdates(byte[] data)
        {
            using (var ms = new MemoryStream(data))
            {
                using (var reader = new EndianBinaryReader(EndianBitConverter.Big, ms))
                {
                    var count = reader.ReadInt32();

                    for (var i = 0; i < count; i++)
                    {
                        var operation = reader.ReadByte();
                        var key = reader.ReadString();

                        if (operation == RemoveOperation)
                        {
                            _values.Remove(key);
                            continue;
                        }

                        var value = reader.ReadSingle();

                        if (_values.ContainsKey(key))
                        {
                            _values[key] = value;
                        }
                        else
                        {
                            _values.Add(key, value);
                        }
                    }
                }
            }
            MarkDirty();
        }

        public override void ClearUpdates()
        {
            _updates.Clear();
        }

        private struct UpdateEntry
        {
            public byte Operation;
            public string Key;
            public float Value;
        }
    }
}