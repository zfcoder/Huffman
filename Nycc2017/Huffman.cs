using System.IO;

// Test commits.

namespace Nycc2017
{
    /// <summary>
    /// Класс позволяет кодировать и декодировать данные, 
    /// </summary>
    public static class Huffman
    {
        /// <summary>
        /// Кодирует данные, полученные из входного потока.
        /// </summary>
        /// <param name="data">Поток с данными.</param>
        /// <returns>Поток с закодированными данными.</returns>
        public static Stream Encode(Stream data)
        {
            var hoffman = new HuffmanCore();
            return hoffman.Encode(data);
        }

        /// <summary>
        /// Декодирует данные, полученные из входного потока.
        /// </summary>
        /// <param name="cypher">Поток с кодированными данными.</param>
        /// <returns>Поток с декодированными данными.</returns>
        public static Stream Decode(Stream cypher)
        {
            var hoffman = new HuffmanCore();
            return hoffman.Decode(cypher);
        }
    }
}
