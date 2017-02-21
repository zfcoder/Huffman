using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Nycc2017
{
    internal class HuffmanCore
    {
        private readonly Dictionary<char, int> _frequencies = new Dictionary<char, int>();

        internal Stream Encode(Stream data)
        {
            var outStream = new MemoryStream();

            if (data.Length == 0){ return outStream; }
            
            var dataText = GetEncoding(data).GetString(ReadFully(data));
            

            if (dataText.Length == 0) { return outStream; }

            var nodes = CalcFrequenies(dataText);

            nodes = BuildCodesTree(nodes);

            var writer = new BinaryWriter(outStream);

            var bitsOfText = new List<bool>();
            foreach (var t in dataText)
            {
                var bits = GetSymbolCode(nodes[0], t, new List<bool>(), ChildNodeDirection.Root);
                for (var j = 0; j < bits.Count; j++)
                {
                    bitsOfText.Add(bits[j]);
                }
            }
            var encodedTextBytes = ConvertToBytes(bitsOfText.ToArray());

            var encodedTree = EncodeNode(nodes[0], new List<bool>());
            var encodedTreeBytes = ConvertToBytes(encodedTree.ToArray());

            writer.Write(BitConverter.GetBytes(bitsOfText.Count));
            writer.Write(BitConverter.GetBytes('\n'));
            writer.Write(BitConverter.GetBytes(encodedTree.Count));
            writer.Write(BitConverter.GetBytes('\n'));
            writer.Write(encodedTreeBytes);
            writer.Write(encodedTextBytes);
            writer.Flush();
            outStream.Flush();
            return outStream;
        }

        public static Encoding GetEncoding(Stream stream)
        {
            var bom = new byte[4];
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(bom, 0, 4);
            stream.Seek(0, SeekOrigin.Begin);

            if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
            if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; 
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode;
            if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            return Encoding.Default;
        }

        private static List<Node> BuildCodesTree(List<Node> nodes)
        {
            while (nodes.Count > 1)
            {
                nodes = nodes.OrderBy(n => n.Frequency).ToList();

                var node = new Node(nodes[0].Frequency + nodes[1].Frequency)
                {
                    LeftChild = nodes[0],
                    RightChild = nodes[1]
                };
                nodes.Remove(nodes[0]);
                nodes.Remove(nodes[0]);
                nodes.Add(node);
            }

            return nodes;
        }

        private List<Node> CalcFrequenies(string dataText)
        {
            foreach (var t in dataText)
            {
                if (!_frequencies.ContainsKey(t))
                {
                    _frequencies.Add(t, 0);
                }
                _frequencies[t]++;
            }
            var nodes = new List<Node>(GetNodesFromFrequence());
            return nodes;
        }

        private static long GetEncodedTextLength(IReadOnlyList<byte> encodedData, out int firstNewLineCharPosition)
        {
            for (var i = 0; i < encodedData.Count; i++)
            {
                try
                {
                    var firstNewLineChar = BitConverter.ToChar(new[] { encodedData[i - 1], encodedData[i] }, 0);
                    if (!firstNewLineChar.Equals('\n')) continue;
                    long textLength = BitConverter.ToInt32(encodedData.Take(i - 1).ToArray(), 0);
                    firstNewLineCharPosition = i;
                    return textLength;
                }
                catch
                {
                    //ignore
                }
            }
            firstNewLineCharPosition = -1;
            return 0;
        }

        private Node GetTreeFromStream(IReadOnlyList<byte> encodedData, int firstNewLineCharPosition, out int endOfTreePosition)
        {
            for (var i = firstNewLineCharPosition + 1; i < encodedData.Count; i++)
            {
                try
                {
                    var secondNewLineChar = BitConverter.ToChar(new[] { encodedData[i - 1], encodedData[i] }, 0);
                    if (!secondNewLineChar.Equals('\n')) continue;
                    var lengthOfTree = BitConverter.ToInt32(encodedData.Skip(firstNewLineCharPosition + 1).Take(i - firstNewLineCharPosition - 2).ToArray(), 0);
                    endOfTreePosition = (i) * 8 + (lengthOfTree % 8 != 0 ? (lengthOfTree / 8 + 2) * 8 : lengthOfTree);
                    var node = ReadNode(new BitReader(new BitArray(encodedData.Skip(i + 1).Take(lengthOfTree / 8 + 1).ToArray()), lengthOfTree));
                    return node;
                }
                catch
                {
                    //ignore
                }
            }
            endOfTreePosition = -1;
            return null;
        }

        public Stream Decode(Stream data)
        {
            data.Seek(0, SeekOrigin.Begin);
            var stream = new MemoryStream();
            var reader = new BinaryReader(data);
            var encodedData = new byte[data.Length];
            reader.Read(encodedData, 0, Convert.ToInt32(data.Length));
            var decoded = new StringBuilder();

            var bits = new BitArray(encodedData);
            if (bits.Length > 0)
            {
                int firstNewLineCharPosition;
                var lengthOfEncodedText = GetEncodedTextLength(encodedData, out firstNewLineCharPosition);
                int endOfTreePosition;
                var rootNode = GetTreeFromStream(encodedData, firstNewLineCharPosition, out endOfTreePosition);
                var current = rootNode;

                encodedData = encodedData.ToArray();
                bits = new BitArray(encodedData);


                for (var i = endOfTreePosition; i < endOfTreePosition + lengthOfEncodedText; i++)
                {
                    var bit = bits[i];
                    if (bit)
                    {
                        if (current.RightChild != null)
                        {
                            current = current.RightChild;
                        }
                    }
                    else
                    {
                        if (current.LeftChild != null)
                        {
                            current = current.LeftChild;
                        }
                    }

                    if (!IsLeafNode(current)) continue;

                    decoded.Append(current.Symbol);
                    current = rootNode;
                }
            }
            var writer = new StreamWriter(stream);
            writer.Write(decoded);
            writer.Flush();
            return stream;
        }

        internal Node ReadNode(BitReader reader)
        {
            if (reader.CanReadByte())
            {
                if (reader.ReadBit())
                {
                    var symbolBytes = new[] { reader.ReadByte(), reader.ReadByte() };
                    return new Node(BitConverter.ToChar(symbolBytes, 0), 0);
                }
                else
                {
                    var leftChild = ReadNode(reader);
                    var rightChild = ReadNode(reader);
                    var parentNode = new Node(0)
                    {
                        RightChild = rightChild,
                        LeftChild = leftChild
                    };
                    return parentNode;
                }
            }
            else
            {
                return null;
            }
        }

        private List<bool> EncodeNode(Node node, List<bool> treeCode)
        {
            if (IsLeafNode(node))
            {
                treeCode.Add(true);
                treeCode.AddRange(GetSymbolBits(node.Symbol));
            }
            else
            {
                treeCode.Add(false);
                EncodeNode(node.LeftChild, treeCode);
                EncodeNode(node.RightChild, treeCode);
            }
            return treeCode;
        }

        private IEnumerable<bool> GetSymbolBits(char symbol)
        {
            var symbolBytes = BitConverter.GetBytes(symbol);
            var symbolBits = new BitArray(symbolBytes);
            return symbolBits.Cast<bool>().ToList();
        }

        private bool IsLeafNode(Node node)
        {
            return node.LeftChild == null && node.RightChild == null;
        }

        private byte[] ConvertToBytes(bool[] bits)
        {
            var a = new BitArray(bits);
            var bytes = new byte[a.Length % 8 != 0 ? a.Length / 8 + 1 : a.Length / 8];
            a.CopyTo(bytes, 0);
            return bytes;
        }

        private BitArray GetSymbolCode(Node node, char symbol, IEnumerable<bool> code, ChildNodeDirection direction)
        {
            var codeOfCurrentSymbol = new List<bool>();
            codeOfCurrentSymbol.AddRange(code);
            if (node == null) return null;
            BitArray result;

            if (direction.Equals(ChildNodeDirection.Left))
            {
                codeOfCurrentSymbol.Add(false);
            }
            else
            {
                if (direction.Equals(ChildNodeDirection.Right))
                {
                    codeOfCurrentSymbol.Add(true);
                }
            }

            if (node.Symbol.CompareTo(symbol) == 0)
            {
                if (direction.Equals(ChildNodeDirection.Root))
                {
                    codeOfCurrentSymbol.Add(false);
                }
                return new BitArray(codeOfCurrentSymbol.ToArray());
            }
            else
            {
                result = GetSymbolCode(node.LeftChild, symbol, codeOfCurrentSymbol, ChildNodeDirection.Left) ??
                         GetSymbolCode(node.RightChild, symbol, codeOfCurrentSymbol, ChildNodeDirection.Right);
            }
            return result;
        }

        private IEnumerable<Node> GetNodesFromFrequence()
        {
            return _frequencies.Select(frequency => new Node(frequency.Key, frequency.Value));
        }

        private byte[] ReadFully(Stream input)
        {
            var buffer = new byte[16 * 1024];
            using (var ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }
    }

    internal class BitReader
    {
        private int _cursor;
        private readonly bool[] _bits;

        public BitReader(BitArray bytes, int bitsOfData)
        {
            _cursor = 0;
            var bitsFromBitArray = new List<bool>();

            for (var i = 0; i < bitsOfData - 1; i++)
            {
                bitsFromBitArray.Add(bytes[i]);
            }
            _bits = bitsFromBitArray.ToArray();
        }

        public bool CanReadByte()
        {
            return _cursor < _bits.Length - 8;
        }

        public bool ReadBit()
        {
            _cursor++;
            return _bits[_cursor - 1];
        }

        public byte ReadByte()
        {
            _cursor += 8;
            return ConvertToBytes(_bits.Skip(_cursor - 8).Take(8).ToArray())[0];
        }

        private byte[] ConvertToBytes(bool[] bits)
        {
            var a = new BitArray(bits);
            var bytes = new byte[a.Length / 8 + 1];
            a.CopyTo(bytes, 0);
            return bytes;
        }
    }

    internal class Node
    {
        public Node LeftChild { get; set; }
        public Node RightChild { get; set; }
        public char Symbol { get; }
        public int Frequency { get; }

        public Node(char symbol, int frequence)
        {
            Symbol = symbol;
            Frequency = frequence;
        }

        public Node(int frequence)
        {
            Frequency = frequence;
        }
    }

    public enum ChildNodeDirection
    {
        Left = 0,
        Right = 1,
        Root = -1
    }
}
