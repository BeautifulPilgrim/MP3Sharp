// /***************************************************************************
//  * BitReserve.cs
//  * Copyright (c) 2015, 2021 The Authors.
//  * 
//  * All rights reserved. This program and the accompanying materials
//  * are made available under the terms of the GNU Lesser General Public License
//  * (LGPL) version 3 which accompanies this distribution, and is available at
//  * https://www.gnu.org/licenses/lgpl-3.0.en.html
//  *
//  * This library is distributed in the hope that it will be useful,
//  * but WITHOUT ANY WARRANTY; without even the implied warranty of
//  * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//  * Lesser General Public License for more details.
//  *
//  ***************************************************************************/

namespace MP3Sharp.Decoding {
    /// <summary>
    /// Implementation of Bit Reservoir for Layer III.
    /// The implementation stores single bits as a word in the buffer. If
    /// a bit is set, the corresponding word in the buffer will be non-zero.
    /// If a bit is clear, the corresponding word is zero. Although this
    /// may seem waseful, this can be a factor of two quicker than
    /// packing 8 bits to a byte and extracting.
    /// </summary>

    // REVIEW: there is no range checking, so buffer underflow or overflow
    // can silently occur.
    internal sealed class BitReserve {
        /// <summary>
        /// Size of the internal buffer to store the reserved bits.
        /// Must be a power of 2. And x8, as each bit is stored as a single
        /// entry.
        /// </summary>
        private const int BUFSIZE_BITS = 4096 * 8;
        private const int BUFSIZE_BYTES = 4096;

        /// <summary>
        /// Mask that can be used to quickly implement the
        /// modulus operation on BUFSIZE.
        /// </summary>
        private const int BUFSIZE_MASK_BYTES = BUFSIZE_BYTES - 1;

        private byte[] _Buffer;
        private int _WriteByteIdx;
        private int _ReadByteIdx;
        private int _ReadBitIdx;
        private int _Totbit;

        internal BitReserve() {
            InitBlock();

            _WriteByteIdx = 0;
            _ReadByteIdx = 0;
            _ReadBitIdx = 0;
            _Totbit = 0;
        }

        private void InitBlock() {
            _Buffer = new byte[BUFSIZE_BYTES];
        }

        /// <summary>
        /// Return totbit Field.
        /// </summary>
        internal int HssTell() => _Totbit;

        /// <summary>
        /// Read a number bits from the bit stream.
        /// </summary>
        internal int ReadBits(int n) {
            _Totbit += n;

            int val = 0;
            int bytePos = _ReadByteIdx;
            int bitPos = _ReadBitIdx;

            // Fast path: consume whole bytes when aligned.
            while (n >= 8 && bitPos == 0) {
                val = (val << 8) | _Buffer[bytePos];
                bytePos = (bytePos + 1) & BUFSIZE_MASK_BYTES;
                n -= 8;
            }

            while (n > 0) {
                int remainingInByte = 8 - bitPos;
                int take = n < remainingInByte ? n : remainingInByte;
                int shift = remainingInByte - take;
                int bits = (_Buffer[bytePos] >> shift) & ((1 << take) - 1);
                val = (val << take) | bits;
                bitPos += take;
                if (bitPos == 8) {
                    bitPos = 0;
                    bytePos = (bytePos + 1) & BUFSIZE_MASK_BYTES;
                }
                n -= take;
            }

            _ReadByteIdx = bytePos;
            _ReadBitIdx = bitPos;
            return val;
        }

        /// <summary>
        /// Read 1 bit from the bit stream.
        /// </summary>
        internal int ReadOneBit() {
            _Totbit++;
            int val = (_Buffer[_ReadByteIdx] >> (7 - _ReadBitIdx)) & 0x1;
            _ReadBitIdx++;
            if (_ReadBitIdx == 8) {
                _ReadBitIdx = 0;
                _ReadByteIdx = (_ReadByteIdx + 1) & BUFSIZE_MASK_BYTES;
            }
            return val;
        }

        /// <summary>
        /// Write 8 bits into the bit stream.
        /// </summary>
        internal void PutBuffer(int val) {
            _Buffer[_WriteByteIdx] = (byte)val;
            _WriteByteIdx = (_WriteByteIdx + 1) & BUFSIZE_MASK_BYTES;
        }

        /// <summary>
        /// Rewind n bits in Stream.
        /// </summary>
        internal void RewindStreamBits(int bitCount) {
            _Totbit -= bitCount;
            int bitPos = (_ReadByteIdx << 3) + _ReadBitIdx - bitCount;
            if (bitPos < 0) {
                bitPos += BUFSIZE_BITS;
            }
            _ReadByteIdx = (bitPos >> 3) & BUFSIZE_MASK_BYTES;
            _ReadBitIdx = bitPos & 7;
        }

        /// <summary>
        /// Rewind n bytes in Stream.
        /// </summary>
        internal void RewindStreamBytes(int byteCount) {
            int bits = byteCount << 3;
            RewindStreamBits(bits);
        }
    }
}
