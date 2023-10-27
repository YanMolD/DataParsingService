using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace DataParsingService
{
    internal static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// для запуска введите в cmd от имени администратора:
        /// cd C:\Windows\Microsoft.NET\Framework64\v4.0.30319
        /// installutil *путь к архиву*\DataParsingService\DataParsingService\bin\Debug\DataParsingService.exe
        /// sc start DataParsingService
        /// </summary>
        static void Main()
        {
            ServiceBase[] ServicesToRun;
            ServicesToRun = new ServiceBase[]
            {
                new ParsingService()
            };
            ServiceBase.Run(ServicesToRun);
        }
    }
}
