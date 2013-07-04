﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace CUDAFingerprinting.Common.SerializationHelper
{
    public class BinarySerializationHelper
    {
        private static BinaryFormatter _formatter = new BinaryFormatter();

        public static byte[] SerializeObject<T>(T toSerialize)
        {
            var ms = new MemoryStream();
            _formatter.Serialize(ms, toSerialize);
            return ms.ToArray();
        }

        public static T DeserializeObject<T>(byte[] toDeserialize)
        {
            return (T)_formatter.Deserialize(new MemoryStream(toDeserialize));
        }
    }
}
