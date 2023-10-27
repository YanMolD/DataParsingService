using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.ServiceProcess;
using System.Text;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Configuration;
using System.Globalization;

namespace DataParsingService
{
    public partial class ParsingService : ServiceBase
    {
        public ParsingService()
        {
            InitializeComponent();
        }

        private string directoryPath = ConfigurationManager.AppSettings["DirectoryPath"];
        private FileSystemWatcher watcherPdf;
        private FileSystemWatcher watcherXml;
        protected override void OnStart(string[] args)
        {
            watcherPdf = new FileSystemWatcher
            {
                Path = directoryPath + "\\In\\",
                NotifyFilter = NotifyFilters.FileName,
                Filter = "*.pdf"
            };
            watcherXml = new FileSystemWatcher
            {
                Path = directoryPath + "\\In\\",
                NotifyFilter = NotifyFilters.FileName,
                Filter = "*.xml"
            };
            watcherXml.Created += new FileSystemEventHandler(OnFileCreated);
            watcherXml.EnableRaisingEvents = true;
            watcherPdf.Created += new FileSystemEventHandler(OnFileCreated);
            watcherPdf.EnableRaisingEvents = true;
        }
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            //FileStream log=File.Open(directoryPath + "\\Log.txt", FileMode.Open , access: FileAccess.ReadWrite,share: FileShare.ReadWrite);
            File.AppendAllText(directoryPath + "\\Log.txt", e.Name + " added to directory\n"); //потенциальное исключение (тут и далее- попытка параллельной записи в лог двух процессов
            try
            {
                string filePath = directoryPath + "\\In\\" + Path.GetFileNameWithoutExtension(e.Name);
                if (Path.GetExtension(e.Name) == ".pdf" ?
                         Directory.GetFiles(directoryPath + "\\In\\").Contains
                         (filePath + ".xml") :
                         Directory.GetFiles(directoryPath + "\\In\\").Contains
                         (filePath + ".pdf"))
                {
                    File.AppendAllText(directoryPath + "\\Log.txt", Path.GetFileNameWithoutExtension(e.Name) + " .xml and .pdf parsing\n");
                    string xmlFilePath = filePath + ".xml";
                    XDocument doc = XDocument.Load(xmlFilePath);
                    string fileName = doc.Descendants("FileName").FirstOrDefault().Value;//потенциальное исключение- отсутствующие поля FileName и UserName
                    string userName = doc.Descendants("UserName").FirstOrDefault().Value;
                    List<User> users = JsonConvert.DeserializeObject<List<User>>(File.ReadAllText(directoryPath + "\\users.json"));
                    User user = users.Find(u => u.userName == userName);
                    File.AppendAllText(directoryPath + "\\Log.txt", Path.GetFileNameWithoutExtension(e.Name) + " users email-" +user.email+ "\n");
                    string dataToAppend = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\t" +
                        userName + "\t" + Path.GetFileNameWithoutExtension(e.Name) + "\t" + user.email + "\t";
                    if (fileName == Path.GetFileNameWithoutExtension(e.Name))
                    {
                        File.AppendAllText(directoryPath + "\\Out\\result.csv", dataToAppend + "true" + "\r\n");
                        File.AppendAllText(directoryPath + "\\Log.txt", Path.GetFileNameWithoutExtension(e.Name) + " result- true\n");
                        File.Copy(filePath + ".pdf", directoryPath + "\\Success\\" + Path.GetFileNameWithoutExtension(e.Name) + ".pdf"); // возможное исключение- повторяющиеся имена файлов
                        File.Delete(filePath + ".pdf");                                                                                  // к сожалению, File.Move выдаёт исключение, указанное выше, проверить на другом компьютере не успел
                        File.AppendAllText(directoryPath + "\\Log.txt", Path.GetFileNameWithoutExtension(e.Name) + ".pdf moved to Success\n");
                        File.Copy(filePath + ".xml", directoryPath + "\\Success\\" + Path.GetFileNameWithoutExtension(e.Name) + ".xml");
                        File.Delete(filePath + ".xml");
                        File.AppendAllText(directoryPath + "\\Log.txt", Path.GetFileNameWithoutExtension(e.Name) + ".xml moved to Success\n");
                    }
                    else
                    {
                        File.AppendAllText(directoryPath + "\\Out\\result.csv", dataToAppend + "false" + "\r\n");

                        File.AppendAllText(directoryPath + "\\Log.txt", Path.GetFileNameWithoutExtension(e.Name) + " result- false\n");
                        File.Copy(filePath + ".pdf", directoryPath + "\\Error\\" + Path.GetFileNameWithoutExtension(e.Name) + ".pdf");
                        File.Delete(filePath + ".pdf");
                        File.AppendAllText(directoryPath + "\\Log.txt", Path.GetFileNameWithoutExtension(e.Name) + ".pdf moved to Error\n");
                        File.Copy(filePath + ".xml", directoryPath + "\\Error\\" + Path.GetFileNameWithoutExtension(e.Name) + ".xml");
                        File.Delete(filePath + ".xml");
                        File.AppendAllText(directoryPath + "\\Log.txt", Path.GetFileNameWithoutExtension(e.Name) + ".xml moved to Error\n");
                    }
                }
            }
            catch (Exception exep)
            {
                File.AppendAllText(directoryPath + "\\Log.txt", "\n" + exep.Message);
            }
        }
        protected override void OnStop()
        {
        }

    }
}
