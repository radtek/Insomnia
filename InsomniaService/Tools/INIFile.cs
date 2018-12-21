using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace MadWizard.Insomnia.Tools
{
    class INIFile
    {
        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string section, string key, string value, string path);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string section, string key, string defaultValue, byte[] buffer, int size, string path);

        public readonly char[] CSV_SPLITTERS = { ',', ';' };

        string path;

        public INIFile(string path)
        {
            this.path = path;
        }

        private string[] Read(string section, string key)
        {
            var buffer = new byte[2048];
            GetPrivateProfileString(section, key, "", buffer, buffer.Length, path);
            string[] data = Encoding.Unicode.GetString(buffer).Trim('\0').Split('\0');
            return data.Length == 1 && data[0].Length == 0 ? new string[0] : data;
        }

        private void Write(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, path);
        }

        public string[] ListSections()
        {
            return Read(null, null);
        }

        public string[] ListKeys(string section)
        {
            if (section == null)
                throw new ArgumentException();

            return Read(section, null);
        }

        public string ReadKey(string section, string key, object defaultValue = null)
        {
            if (section == null || key == null)
                throw new ArgumentException();

            string[] data = Read(section, key);

            return data.Length > 0 ? data[0] : defaultValue != null ? defaultValue.ToString() : null;
        }

        public string[] ReadKeyAsCSV(string section, string key)
        {
            string values = ReadKey(section, key, "");
            return values.Split(CSV_SPLITTERS).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        }

        public bool KeyExists(string section, string key)
        {
            return ReadKey(key, section).Length > 0;
        }

        public void WriteKey(string section, string key, object value)
        {
            if (section == null || key == null)
                throw new ArgumentException();

            Write(section, key, value.ToString());
        }

        public void DeleteKey(string section, string key)
        {
            if (section == null || key == null)
                throw new ArgumentException();

            Write(section, key, null);
        }

        public void DeleteSection(string section)
        {
            if (section == null)
                throw new ArgumentException();

            Write(section, null, null);
        }
    }
}