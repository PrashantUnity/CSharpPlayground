using System;
using System.IO;

namespace PdfEditorApp.Plugins.CSharpEditor.Services.Media.Gif;

/// <summary>
/// Variable bit-length LZW compressor for GIF image data sub-blocks according to GIF89a specification.
/// Implements standard Knuth open-addressing double-hashing with adaptive table resets.
/// </summary>
public sealed class LzwEncoder
{
    private const int Bits = 12;
    private const int HSize = 5003; // 80% occupancy for fast hash probing
    private const int MaxMaxCode = 1 << Bits; // 4096

    private static readonly int[] Masks =
    [
        0x0000, 0x0001, 0x0003, 0x0007, 0x000F, 0x001F, 0x003F, 0x007F,
        0x00FF, 0x01FF, 0x03FF, 0x07FF, 0x0FFF, 0x1FFF, 0x3FFF, 0x7FFF, 0xFFFF
    ];

    private readonly int _imgW;
    private readonly int _imgH;
    private readonly byte[] _pixAry;
    private readonly int _initCodeSize;

    private int _remaining;
    private int _curPixel;

    private int _nBits;
    private int _maxCode;
    private int _freeEnt;
    private bool _clearFlg;
    private int _gInitBits;
    private int _clearCode;
    private int _eofCode;

    private int _curAccum;
    private int _curBits;

    private int _aCount;
    private readonly byte[] _accum = new byte[256];

    private readonly int[] _htab = new int[HSize];
    private readonly int[] _codetab = new int[HSize];

    public LzwEncoder(int width, int height, byte[] pixels, int colorDepth)
    {
        _imgW = width;
        _imgH = height;
        _pixAry = pixels;
        _initCodeSize = Math.Max(2, colorDepth);
    }

    public void Encode(Stream stream)
    {
        stream.WriteByte((byte)_initCodeSize);
        _remaining = _imgW * _imgH;
        _curPixel = 0;
        _curAccum = 0;
        _curBits = 0;
        _aCount = 0;

        Compress(_initCodeSize + 1, stream);
        stream.WriteByte(0x00); // Block terminator
    }

    private int NextPixel()
    {
        if (_remaining == 0) return -1;
        _remaining--;
        return _pixAry[_curPixel++] & 0xFF;
    }

    private void Compress(int initBits, Stream stream)
    {
        _gInitBits = initBits;
        _clearFlg = false;
        _nBits = _gInitBits;
        _maxCode = MaxCode(_nBits);

        _clearCode = 1 << (initBits - 1);
        _eofCode = _clearCode + 1;
        _freeEnt = _clearCode + 2;

        _aCount = 0;

        int ent = NextPixel();

        int hshift = 0;
        for (int fcode = HSize; fcode < 65536; fcode *= 2)
            ++hshift;
        hshift = 8 - hshift;

        int hsizeReg = HSize;
        ClearHash();

        Output(_clearCode, stream);

        int c;
        while ((c = NextPixel()) != -1)
        {
            int fcode = (c << Bits) + ent;
            int i = (c << hshift) ^ ent;

            if (_htab[i] == fcode)
            {
                ent = _codetab[i];
                continue;
            }

            if (_htab[i] >= 0)
            {
                int disp = hsizeReg - i;
                if (i == 0) disp = 1;

                bool found = false;
                do
                {
                    if ((i -= disp) < 0)
                        i += hsizeReg;

                    if (_htab[i] == fcode)
                    {
                        ent = _codetab[i];
                        found = true;
                        break;
                    }
                } while (_htab[i] >= 0);

                if (found) continue;
            }

            Output(ent, stream);
            ent = c;

            if (_freeEnt < MaxMaxCode)
            {
                _codetab[i] = _freeEnt++;
                _htab[i] = fcode;
            }
            else
            {
                ClearBlock(stream);
            }
        }

        Output(ent, stream);
        Output(_eofCode, stream);
    }

    private void ClearHash()
    {
        Array.Fill(_htab, -1);
    }

    private void ClearBlock(Stream stream)
    {
        ClearHash();
        _freeEnt = _clearCode + 2;
        _clearFlg = true;
        Output(_clearCode, stream);
    }

    private void Output(int code, Stream stream)
    {
        _curAccum &= Masks[_curBits];

        if (_curBits > 0)
            _curAccum |= (code << _curBits);
        else
            _curAccum = code;

        _curBits += _nBits;

        while (_curBits >= 8)
        {
            AddChar((byte)(_curAccum & 0xFF), stream);
            _curAccum >>= 8;
            _curBits -= 8;
        }

        if (_freeEnt > _maxCode || _clearFlg)
        {
            if (_clearFlg)
            {
                _maxCode = MaxCode(_nBits = _gInitBits);
                _clearFlg = false;
            }
            else
            {
                ++_nBits;
                if (_nBits == Bits)
                    _maxCode = MaxMaxCode;
                else
                    _maxCode = MaxCode(_nBits);
            }
        }

        if (code == _eofCode)
        {
            while (_curBits > 0)
            {
                AddChar((byte)(_curAccum & 0xFF), stream);
                _curAccum >>= 8;
                _curBits -= 8;
            }

            FlushBlock(stream);
        }
    }

    private void AddChar(byte c, Stream stream)
    {
        _accum[_aCount++] = c;
        if (_aCount >= 254)
            FlushBlock(stream);
    }

    private void FlushBlock(Stream stream)
    {
        if (_aCount > 0)
        {
            stream.WriteByte((byte)_aCount);
            stream.Write(_accum, 0, _aCount);
            _aCount = 0;
        }
    }

    private static int MaxCode(int nBits) => (1 << nBits) - 1;
}
