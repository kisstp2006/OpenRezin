// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Foundation.Logger;


namespace Foundation.IO
{
    public class FileSystem
    {
        public static bool Exists(string path)
        {
            return File.Exists(path);
        }
        public static void Save(string path, byte[] data)
        {
            using (var writer = new FileWriter())
            {
                try
                {
                    if (!writer.Open(path)) return;

                }
                catch (Exception e) 
                {
                    Log.Error("Failed to save file: " + e.Message);
                    return;
                }
                writer.WriteBytes(data, 0, data.Length);
            }
        }
        public static byte[] Load(string path)
        {
            using var reader = new FileReader();

            if (!reader.Open(path))
                return Array.Empty<byte>();

            using var memory = new MemoryStream();
            byte[] chunk = new byte[4096];

            while (!reader.IsEOF())
            {
                int bytesRead = reader.ReadBytes(chunk, 0, chunk.Length);
                if (bytesRead == 0)
                    break;

                memory.Write(chunk, 0, bytesRead);
            }

            return memory.ToArray();
        }
    }
}
