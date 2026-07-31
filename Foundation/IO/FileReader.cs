using Foundation.Logger;
using System;
using System.Collections.Generic;
using System.Text;

namespace Foundation.IO
{
    public class FileReader : IDisposable
    {
        private FileStream? stream;
        private bool eof = true;

        public bool Open(string path)
        {
            try
            {
                stream = new FileStream(path, FileMode.Open, FileAccess.Read);
                eof = false;
                return true;

            }
            catch(IOException e)
            {
                Log.Error("Failed to read file: "+e.Message);
                eof = true;
                return false;
            }
        }

        public void Close() 
        {
            if (stream == null) return;
            try
            {
                stream.Close();
                eof = true;
            }
            catch (IOException e) 
            {
                Log.Error("Failed to close file: " + e.Message);
                eof = true;

            }

        }
        public bool IsEOF() { return eof; }

        public int ReadBytes(byte[] buffer, int offset, int count) 
        {
            if(stream == null) return 0;
            int bytesRead;

            try
            {
                bytesRead = stream.Read(buffer, offset, count);
            }
            catch (IOException e)
            {
                Log.Error("Failed to read bytes: " + e.Message);
                return 0;
            }

            if (bytesRead == 0)
                eof = true;

            return bytesRead;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
