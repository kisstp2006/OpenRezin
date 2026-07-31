// Copyright (c) 2026 Kiss Tibor Péter
// Dual-licensed under the MIT License and MIT No Attribution (MIT-0) — see LICENSE.txt

using Foundation.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation.IO
{
    public class FileWriter : IDisposable
    {
        private FileStream? stream;

        public bool Open(string path)
        {
            try
            {
                stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.Write);
                return true;

            }
            catch (IOException e)
            {
                Log.Error("Failed to open file: " + e.Message);

                return false;
            }
        }

        public void Close()
        {
            if (stream == null) return;
            try
            {
                stream.Close();
            }
            catch (IOException e)
            {
                Log.Error("Failed to close file: " + e.Message);

            }

        }

        public void WriteBytes(byte[] bytes, int offset, int count)
        {
            if (stream == null) return;
            try
            {
                stream.Write(bytes, offset, count);
            }
            catch (IOException e)
            {
                Log.Error("Failed to write bytes: " + e.Message);
                return;
            }
        }


        public void Dispose()
        {
            Close();
        }
    }
}
