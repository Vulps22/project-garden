using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace GrowAGarden
{
    // Alpha V2.0 [2025, 10, 07]
    public class BytesReader
    {
        public const int Vector2Size = 2 * sizeof(float);
        public const int Vector3Size = 3 * sizeof(float);
        public const int Vector4Size = 4 * sizeof(float);
        public const int QuaternionSize = 4 * sizeof(float);
        public const int Color32Size = 4;
        public const int ColorSize = 4 * sizeof(float);

        private readonly byte[] _data;
        private int _position = 0;
        bool _isValid = true;

        public BytesReader(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                _isValid = false;
                _data = Array.Empty<byte>();
                _position = 0;
                return;
            }
            _data = data;
            _position = 0;
            _isValid = true;
        }

        public bool IsValid => _isValid;
        public bool IsEmpty => _data.Length == 0;
        public int BytesRemaining => _isValid ? _data.Length - _position : 0;

        // Decoders

        public byte NextByte()
        {
            if (!Validate(sizeof(byte)))
                return default;

            return _data[_position++];
        }

        public short NextShort()
        {
            if (!Validate(sizeof(short)))
                return default;

            var value = MemoryMarshal.Read<short>(_data.AsSpan(_position, sizeof(short)));
            _position += sizeof(short);
            return value;
        }

        public int NextInt()
        {
            if (!Validate(sizeof(int)))
                return default;

            var value = MemoryMarshal.Read<int>(_data.AsSpan(_position, sizeof(int)));
            _position += sizeof(int);
            return value;
        }

        public float NextFloat()
        {
            if (!Validate(sizeof(float)))
                return default;

            var value = MemoryMarshal.Read<float>(_data.AsSpan(_position, sizeof(float)));
            _position += sizeof(float);
            return value;
        }

        public Vector2 NextVector2()
        {
            if (!Validate(Vector2Size))
                return default;

            var span = _data.AsSpan(_position, Vector2Size);
            var floats = MemoryMarshal.Cast<byte, float>(span);
            _position += Vector2Size;
            return new Vector2(floats[0], floats[1]);
        }

        public Vector3 NextVector3()
        {
            if (!Validate(Vector3Size))
                return default;

            var span = _data.AsSpan(_position, Vector3Size);
            var floats = MemoryMarshal.Cast<byte, float>(span);
            _position += Vector3Size;
            return new Vector3(floats[0], floats[1], floats[2]);
        }

        public Vector4 NextVector4()
        {
            if (!Validate(Vector4Size))
                return default;

            var span = _data.AsSpan(_position, Vector4Size);
            var floats = MemoryMarshal.Cast<byte, float>(span);
            _position += Vector4Size;
            return new Vector4(floats[0], floats[1], floats[2], floats[3]);
        }

        public Quaternion NextQuaternion()
        {
            if (!Validate(QuaternionSize))
                return default;

            var span = _data.AsSpan(_position, QuaternionSize);
            var floats = MemoryMarshal.Cast<byte, float>(span);
            _position += QuaternionSize;
            return new Quaternion(floats[0], floats[1], floats[2], floats[3]);
        }

        public Color32 NextColor32()
        {
            if (!Validate(Color32Size))
                return default;

            var color = new Color32(
                _data[_position],
                _data[_position + 1],
                _data[_position + 2],
                _data[_position + 3]);
            _position += Color32Size;
            return color;
        }

        public Color NextColor()
        {
            if (!Validate(ColorSize))
                return default;

            var span = _data.AsSpan(_position, ColorSize);
            var floats = MemoryMarshal.Cast<byte, float>(span);
            _position += ColorSize;
            return new Color(floats[0], floats[1], floats[2], floats[3]);
        }

        public string NextString(int length)
        {
            if (!Validate(length, length))
                return string.Empty;

            var result = Encoding.UTF8.GetString(_data, _position, length);
            _position += length;
            return result;
        }

        public byte[] NextByteArray(int length)
        {
            int byteCount = sizeof(byte) * length;
            if (!Validate(byteCount, length))
                return Array.Empty<byte>();

            var result = new byte[length];
            Buffer.BlockCopy(_data, _position, result, 0, byteCount);
            _position += byteCount;
            return result;
        }

        public short[] NextShortArray(int length)
        {
            int byteCount = sizeof(short) * length;
            if (!Validate(byteCount, length))
                return Array.Empty<short>();

            var span = _data.AsSpan(_position, byteCount);
            var result = MemoryMarshal.Cast<byte, short>(span).ToArray();
            _position += byteCount;
            return result;
        }

        public int[] NextIntArray(int length)
        {
            int byteCount = sizeof(int) * length;
            if (!Validate(byteCount, length))
                return Array.Empty<int>();

            var span = _data.AsSpan(_position, byteCount);
            var result = MemoryMarshal.Cast<byte, int>(span).ToArray();
            _position += byteCount;
            return result;
        }

        public float[] NextFloatArray(int length)
        {
            int byteCount = sizeof(float) * length;
            if (!Validate(byteCount, length))
                return Array.Empty<float>();

            var span = _data.AsSpan(_position, byteCount);
            var result = MemoryMarshal.Cast<byte, float>(span).ToArray();
            _position += byteCount;
            return result;
        }

        // Privates

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Validate(int bytesCount)
        {
            if (!_isValid || bytesCount <= 0 || _position + bytesCount > _data.Length)
            {
                _isValid = false;
                return false;
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Validate(int bytesCount, int arrayLength)
        {
            if (!_isValid || bytesCount <= 0 || _position + bytesCount > _data.Length || arrayLength <= 0)
            {
                _isValid = false;
                Debug.LogError($"[{nameof(BytesWriter)}]: Buffer inconstancy error!");
                return false;
            }
            return true;
        }
    }
}
