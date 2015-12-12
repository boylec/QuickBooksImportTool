using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QboImporterTool
{
    public sealed class Logger
    {
        // ReSharper disable once InconsistentNaming
        private static Logger _instance;
        private readonly string _filePath;

        public static Logger Instance
        {
            get
            {
                
                var directory = Path.GetFullPath("Logs");
                if(!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                var fullPath = Path.GetFullPath("Logs/ImportLog_" + DateTime.Now.ToString().Replace("/","").Replace(":","") + ".txt");
                return _instance ?? (_instance = new Logger(fullPath));
            }
        }

        public Logger(string filePath)
        {
            _filePath = filePath;
        }

        public void Log(string message)
        {

            using (var streamWriter = new StreamWriter(_filePath,true))
            {

                streamWriter.WriteLine(DateTime.Now + ": " + message + "\r\n");

                streamWriter.Close();
            }
        }
    }
}
