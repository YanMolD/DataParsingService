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
using System.Text.RegularExpressions;
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
        private static object fileLock = new object();
        private string directoryPath = ConfigurationManager.AppSettings["DirectoryPath"];
        private FileSystemWatcher watcher;
        private string logPath = ConfigurationManager.AppSettings["LogPath"];
        protected override void OnStart(string[] args)
        {
            watcher = new FileSystemWatcher
            {
                Path = directoryPath + "\\In\\",
                NotifyFilter = NotifyFilters.FileName,
                Filter = "*.*"
            };
            watcher.Created += new FileSystemEventHandler(OnFileCreated);
            watcher.EnableRaisingEvents = true;
        }
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (Path.GetExtension(e.Name) != ".pdf" && Path.GetExtension(e.Name) != ".xml")
                    throw new ArgumentException("Файл не удовлетворяет условиям фильтра");
                WriteToFile(logPath, $"{DateTime.UtcNow}\t{e.FullPath} added to directory");
                string filesName = Path.GetFileNameWithoutExtension(e.Name);
                string filePath = directoryPath + "\\In\\" + filesName;
                if (File.Exists($"{filePath}.pdf") &&
                    File.Exists($"{filePath}.xml"))
                {
                    WriteToFile(logPath, $"{DateTime.UtcNow}\t{filesName} .xml and .pdf parsing");
                    string xmlFilePath = filePath + ".xml";
                    XDocument doc = XDocument.Load(xmlFilePath);
                    var xmlData = doc.Descendants("Document");
                    if (xmlData == null)
                        throw new InvalidOperationException("Данные в xml-файле отсутствуют");
                    if (xmlData.First().Element("FileName") == null || xmlData.First().Element("UserName") == null)
                        throw new ArgumentException("Оформление xml-файла не соответствует шаблону");
                    bool result = (xmlData.First().Element("FileName").Value == filesName);
                    string resultDirectory = directoryPath + (result ? "\\Success\\" : "\\Error\\");
                    WriteToFile(logPath, $"{DateTime.UtcNow}\t{filesName} result- {result}");
                    bool doesExist = File.Exists($"{resultDirectory}{filesName}.pdf") ||
                                     File.Exists($"{resultDirectory}{filesName}.xml");
                    if (doesExist) filesName = RenameFiles(filesName, resultDirectory);
                    MoveFiles(resultDirectory, filePath, filesName);
                    if (doesExist && result)
                    {
                        xmlData.First().Element("FileName").Value = filesName;
                        xmlData.First().Save(resultDirectory + filesName + ".xml");
                    }
                    WriteToFile(logPath, doesExist ? $"{DateTime.UtcNow}\t{Path.GetFileNameWithoutExtension(e.Name)}.pdf renamed to {filesName} and moved to {resultDirectory}" :
                                                     $"{DateTime.UtcNow}\t{filesName}.pdf moved to to {resultDirectory}");
                    WriteToFile(logPath, doesExist ? $"{DateTime.UtcNow}\t{Path.GetFileNameWithoutExtension(e.Name)}.xml renamed to {filesName} and moved to {resultDirectory}" :
                                                     $"{DateTime.UtcNow}\t{filesName}.xml moved to to {resultDirectory}");
                    List<User> users = JsonConvert.DeserializeObject<List<User>>(File.ReadAllText(directoryPath + "\\users.json"));
                    User user = users.Find(u => u.userName == xmlData.First().Element("UserName").Value);
                    //if (user == null) { } 
                    ///добавление нового юзера
                    ///или несанкционированный доступ, следовательно UnauthorizedAccessException.
                    ///но тогда десериализация json файла проводится сразу после xml-файла
                    string dataToAppend = $"{DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)}\t{xmlData.First().Element("UserName").Value}\t{filesName}\t{(user != null ? user.email + "\t" : "")}";
                    WriteToFile(directoryPath + "\\Out\\result.csv", $"{dataToAppend}{result}\r\n");
                }
            }
            catch (FileNotFoundException ex) ///появляется при в случае двойной обработки одной пары файлов 
            {                                ///причину дублирования найти пока не удалось (наследуется от IOException)
                WriteToFile(logPath, $"\n{DateTime.UtcNow}\tFileNotFoundException:  {ex.Message}\n");
            }
            catch (IOException ex)          ///возникает при том же самом дублировании в момент копирования файла и
            {                               ///при попытке внести изменения в xml файл
                WriteToFile(logPath, $"\n{DateTime.UtcNow}\tIOException :  {ex.Message}");
            }
            catch (UnauthorizedAccessException ex) ///сталкивался один раз при чтении файла формата pdf
            {
                WriteToFile(logPath, $"\n{DateTime.UtcNow}\tUnauthorizedAccessException:  {ex.Message}\n");
            }
            catch (ArgumentException ex) ///на случай добавления файла с неправильным расширением
            {
                WriteToFile(logPath, $"\n{DateTime.UtcNow}\tArgumentException:  {ex.Message}\n");
            }

            catch (InvalidOperationException ex)
            {
                WriteToFile(logPath, $"\n{DateTime.UtcNow}\tInvalidOperationException:  {ex.Message}\n");
            }
            catch (Exception ex)    ///для обработки остальных исключений
            {
                WriteToFile(logPath, $"\n{DateTime.UtcNow}\t{ex.GetType().Name} :  {ex.Message}\n");
            }
        }
        private string RenameFiles(string filesName, string resultDirectory)
        {
            string newFilesName = filesName;
            //string pattern = @"\((\d+)\)$";                                       ///с помощью регулярного выражения вместо *имя_файла*(1)(1)
            //Match match = Regex.Match(filesName, pattern);                        ///(появляется при попытке скопировать в папку файл с именем *имя_файла*(1))
            //int i = match.Success ? Convert.ToInt32(match.Groups[1].Value) : 1;   ///будет создаваться файл *имя_файла*(2)
            int i = 1;
            while (File.Exists($"{resultDirectory}{newFilesName}.pdf") &&
                   File.Exists($"{resultDirectory}{newFilesName}.xml"))
            {
                newFilesName = filesName + $"({i})";
                //newFilesName = Regex.Replace(filesName, pattern, $"({i})");
                i++;
            }
            return newFilesName;
        }
        private void MoveFiles(string resultDirectory, string filePath, string filesName)
        {
            try
            {
                MoveFile(filePath, resultDirectory, filesName, ".pdf");
                MoveFile(filePath, resultDirectory, filesName, ".xml");
            }
            catch (FileNotFoundException ex) ///появляется при в случае двойной обработки одной пары файлов 
            {                                ///причину дублирования найти пока не удалось (наследуется от IOException)
                WriteToFile(logPath, $"\n{DateTime.UtcNow}\tFileNotFoundException:  {ex.Message}\n");
            }
            catch (IOException ex)          ///возникает при том же самом дублировании в момент копирования файла и
            {                               ///при попытке внести изменения в xml файл
                WriteToFile(logPath, $"\n{DateTime.UtcNow}\tIOException :  {ex.Message}");
            }
        }
        private void WriteToFile(string path, string message)
        {
            lock (fileLock)
            {
                using (StreamWriter writer = new StreamWriter(path, true))
                {
                    writer.WriteLine(message);
                }
            }
        }
        private void MoveFile(string filePath, string path, string filesName, string extention)
        {
            File.Copy(filePath + extention, path + filesName + extention);
            File.Delete(filePath + extention);
        }
        protected override void OnStop()
        {
        }

    }
}
