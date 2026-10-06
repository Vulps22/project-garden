using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GrowAGarden.Community
{
    // Alpha V2.0 [2025, 10, 07]
    public class BytesWriter 
    {
        public const int ByteSize = 1;
        public const int ShortSize = sizeof(short);
        public const int FloatSize = sizeof(float);
        public const int IntSize = sizeof(int);
        public const int Vector2Size = 2 * sizeof(float);
        public const int Vector3Size = 3 * sizeof(float);
        public const int Vector4Size = 4 * sizeof(float);
        public const int QuaternionSize = 4 * sizeof(float);
        public const int Color32Size = 4;
        public const int ColorSize = 4 * sizeof(float);

        private readonly byte[] _data;
        private int _position = 0;
        private bool _isValid = true;

        public BytesWriter(int bytesSize)
        {
            if (bytesSize <= 0)
            {
                _isValid = false;
                _data = Array.Empty<byte>();
                Debug.LogError($"[{nameof(BytesWriter)}]: Constructor received non-positive size.");
                return;
            }

            _data = new byte[bytesSize];
        }

        public bool IsValid => _isValid;
        public bool IsFull => _position >= _data.Length;
        public byte[] Data => _data;

        /// <summary>
        /// Byte is between 0 and 255
        /// </summary>
        public void AddByte(byte source)
        {
            if (!Validate(sizeof(byte)))
                return;

            _data[_position++] = source;
        }

        /// <summary>
        /// short is between -32,768 to 32,767
        /// </summary>
        public void AddShort(short source)
        {
            if (!Validate(sizeof(short)))
                return;

            Span<byte> bufferSpan = _data.AsSpan(_position, sizeof(short));
            MemoryMarshal.Write(bufferSpan, ref source);
            _position += sizeof(short);
        }

        /// <summary>
        /// int is between -2,147,483,648 to 2,147,483,647
        /// </summary>
        public void AddInt(int source)
        {
            if (!Validate(sizeof(int)))
                return;

            Span<byte> bufferSpan = _data.AsSpan(_position, sizeof(int));
            MemoryMarshal.Write(bufferSpan, ref source);
            _position += sizeof(int);
        }

        public void AddFloat(float source)
        {
            if (!Validate(sizeof(float)))
                return;

            Span<byte> bufferSpan = _data.AsSpan(_position, sizeof(float));
            MemoryMarshal.Write(bufferSpan, ref source);
            _position += sizeof(float);
        }

        public void AddVector2(Vector2 source)
        {
            if (!Validate(Vector2Size)) 
                return;

            Span<byte> bufferSpan = _data.AsSpan(_position, Vector2Size);
            MemoryMarshal.Write(bufferSpan.Slice(0 * sizeof(float)), ref source.x);
            MemoryMarshal.Write(bufferSpan.Slice(1 * sizeof(float)), ref source.y);
            _position += Vector2Size;
        }

        public void AddVector3(Vector3 source)
        {
            if (!Validate(Vector3Size))
                return;

            Span<byte> bufferSpan = _data.AsSpan(_position, Vector3Size);
            MemoryMarshal.Write(bufferSpan, ref source.x);
            MemoryMarshal.Write(bufferSpan.Slice(sizeof(float)), ref source.y);
            MemoryMarshal.Write(bufferSpan.Slice(2 * sizeof(float)), ref source.z);
            _position += Vector3Size;
        }

        public void AddVector4(Vector4 source)
        {
            if (!Validate(Vector4Size))
                return;

            Span<byte> bufferSpan = _data.AsSpan(_position, Vector4Size);
            MemoryMarshal.Write(bufferSpan, ref source.x);
            MemoryMarshal.Write(bufferSpan.Slice(sizeof(float)), ref source.y);
            MemoryMarshal.Write(bufferSpan.Slice(2 * sizeof(float)), ref source.z);
            MemoryMarshal.Write(bufferSpan.Slice(3 * sizeof(float)), ref source.w);
            _position += Vector4Size;
        }

        public void AddQuaternion(Quaternion source)
        {
            if (!Validate(QuaternionSize))
                return;

            Span<byte> bufferSpan = _data.AsSpan(_position, QuaternionSize);
            MemoryMarshal.Write(bufferSpan, ref source.x);
            MemoryMarshal.Write(bufferSpan.Slice(sizeof(float)), ref source.y);
            MemoryMarshal.Write(bufferSpan.Slice(2 * sizeof(float)), ref source.z);
            MemoryMarshal.Write(bufferSpan.Slice(3 * sizeof(float)), ref source.w);
            _position += QuaternionSize;
        }

        public void AddColor32(Color32 source)
        {
            if (!Validate(Color32Size)) 
                return;

            Span<byte> bufferSpan = _data.AsSpan(_position, Color32Size);
            bufferSpan[0] = source.r;
            bufferSpan[1] = source.g;
            bufferSpan[2] = source.b;
            bufferSpan[3] = source.a;
            _position += Color32Size;
        }

        public void AddColor(Color source)
        {
            if (!Validate(ColorSize))
                return;

            Span<byte> bufferSpan = _data.AsSpan(_position, ColorSize);
            MemoryMarshal.Write(bufferSpan, ref source.r);
            MemoryMarshal.Write(bufferSpan.Slice(sizeof(float)), ref source.g);
            MemoryMarshal.Write(bufferSpan.Slice(2 * sizeof(float)), ref source.b);
            MemoryMarshal.Write(bufferSpan.Slice(3 * sizeof(float)), ref source.a);
            _position += ColorSize;
        }

        public void AddByteArray(byte[] source)
        {
            if (source == null)
            {
                _isValid = false;
                Debug.LogError($"[{nameof(BytesWriter)}]: AddByteArray received null.");
                return;
            }
            if (source.Length == 0) // We do nothing on empty arrays
                return;

            if (!Validate(source.Length))
                return;

            Buffer.BlockCopy(source, 0, _data, _position, source.Length);
            _position += source.Length;
        }

        public void AddShortArray(short[] source)
        {
            if (source == null)
            {
                _isValid = false;
                Debug.LogError($"[{nameof(BytesWriter)}]: AddShortArray received null.");
                return;
            }
            if (source.Length == 0) // We do nothing on empty arrays
                return;

            int bytes = source.Length * sizeof(short);
            if (!Validate(bytes)) 
                return;

            Buffer.BlockCopy(source, 0, _data, _position, bytes);
            _position += bytes;
        }

        public void AddIntArray(int[] source)
        {
            if (source == null)
            {
                _isValid = false;
                Debug.LogError($"[{nameof(BytesWriter)}]: AddIntArray received null.");
                return;
            }
            if (source.Length == 0) // We do nothing on empty arrays
                return;

            int bytes = source.Length * sizeof(int);
            if (!Validate(bytes)) 
                return;

            Buffer.BlockCopy(source, 0, _data, _position, bytes);
            _position += bytes;
        }

        public void AddFloatArray(float[] source)
        {
            if (source == null)
            {
                _isValid = false;
                Debug.LogError($"[{nameof(BytesWriter)}]: AddFloatArray received null.");
                return;
            }
            if (source.Length == 0) // We do nothing on empty arrays
                return;

            int bytes = source.Length * sizeof(float);
            if (!Validate(bytes)) 
                return;

            Buffer.BlockCopy(source, 0, _data, _position, bytes);
            _position += bytes;
        }

        /// <summary> 
        /// Convert string to UTF-8 bytes, size in bytes can be unpredictable. 
        /// String max size is short.MaxValue
        /// </summary>
        public void AddString(string source, int byteLength)
        {
            if (source == null)
            {
                _isValid = false;
                Debug.LogError($"[{nameof(BytesWriter)}]: AddString received null.");
                return;
            }

            byte[] stringData = System.Text.Encoding.UTF8.GetBytes(source);

            if (stringData.Length > byteLength)
            {
                Debug.LogWarning($"[{nameof(BytesWriter)}]: String UTF8 byte length > byteLength. {stringData.Length} > {byteLength}, string will be truncated");
            }

            if (!Validate(byteLength))
                return;

            int readLength = Math.Min(stringData.Length, byteLength);
            // Write data
            Buffer.BlockCopy(stringData, 0, _data, _position, readLength);

            _position += byteLength;
        }

        /// <summary> 
        /// Convert string to UTF-8 bytes, size in bytes can be unpredictable. 
        /// First will write byte count as short then data. Max string length is 32767 bytes (not chars) due to short limit. 
        /// </summary>
        public void AddAutoString(string source)
        {
            if (source == null)
            {
                _isValid = false;
                Debug.LogError($"[{nameof(BytesWriter)}]: AddString received null.");
                return;
            }

            byte[] stringData = System.Text.Encoding.UTF8.GetBytes(source);

            if (!Validate(sizeof(short) + stringData.Length) || stringData.Length > short.MaxValue)
                return;

            // Write byte count as short
            short byteCount = (short)stringData.Length;
            Span<byte> bufferSpan = _data.AsSpan(_position, sizeof(short));
            MemoryMarshal.Write(bufferSpan, ref byteCount);
            _position += sizeof(short);
            // Write data
            Buffer.BlockCopy(stringData, 0, _data, _position, byteCount);
            _position += byteCount;
        }

        // Privates

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Validate(int bytesCount)
        {
            if (!_isValid || bytesCount < 0 || _position + bytesCount > _data.Length)
            {
                _isValid = false;
                Debug.LogErrorFormat("[{0}]: Buffer inconsistency. pos={1} need={2} len={3}", nameof(BytesWriter), _position, bytesCount, _data.Length);
                return false;
            }
            return true;
        }
    }
}
