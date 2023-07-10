using CommandLine;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace RenameImages
{
    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void Main(string[] args)
        {
            try
            {
                string dirPath;
                if (args.Length == 0)
                {
                    dirPath = Environment.CurrentDirectory;
                }
                else
                    dirPath = args[0];

                CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args)
                    .WithParsed(options =>
                {
                    dirPath = options.Path;

                });




                log.DebugFormat("Working direcory is: {0}", dirPath);

                DirectoryInfo dir = new DirectoryInfo(dirPath);
                log.Info("Proccessing files");

                CultureInfo provider = CultureInfo.InvariantCulture;
                string dateFormats = Properties.Settings.Default.dateFormat;


                Action<FileInfo> renameFileAction = file =>
                {
                    if (!file.Extension.Equals(".jpg", StringComparison.CurrentCultureIgnoreCase))
                        return;

                    string name = file.Name.Replace(file.Extension, "");

                    DateTime dateTaken;

                    // check if the pattern is valid for this file
                    if (DateTime.TryParseExact(name, dateFormats, provider, DateTimeStyles.AllowWhiteSpaces, out dateTaken))
                        return;


                    Image imageToRename = null;
                    try
                    {
                        imageToRename = Image.FromFile(file.FullName);
                    }
                    catch (OutOfMemoryException)
                    {
                        log.WarnFormat("Not abel to load as an image: {0}", file.FullName);
                        return;
                    }
                    catch (FileNotFoundException)
                    {
                        log.WarnFormat("File no longer present: {0}", file.FullName);
                        return;
                    }
                    catch (Exception exception)
                    {
                        log.Error("Exception when trying to read the file as an Image: " + file.FullName, exception);
                        return;
                    }

                    dateTaken = GetDateTaken(imageToRename, file);

                    imageToRename.Dispose();

                    RenameFile(file, dateFormats, dateTaken);

                };

                DirTraverse(dir, renameFileAction);

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        private static void RenameFile(FileInfo file, string dateFormats, DateTime dateTaken, int index = 0)
        {
            string suggestedName = string.Empty;
            if (dateTaken != DateTime.MinValue)
                try
                {
                    if (index == 0)
                    {
                        suggestedName = file.Directory.FullName + "\\" + dateTaken.ToString(dateFormats) + file.Extension;
                    }
                    else
                    {
                        suggestedName = file.Directory.FullName + "\\" + dateTaken.ToString(dateFormats) + "-" + index + file.Extension;
                    }

                    file.MoveTo(suggestedName);
                }
                catch (Exception)
                {
                    log.ErrorFormat("File with the same name exists: {0}", suggestedName);
                    RenameFile(file, dateFormats, dateTaken, index + 1);
                    return;
                }
        }

        public static DateTime GetDateTaken(Image targetImg, FileInfo fileinfo)
        {
            try
            {
                //Property Item 36867 corresponds to the Date Taken
                PropertyItem propItem = targetImg.GetPropertyItem(36867);
                DateTime dtaken;

                //Convert date taken metadata to a DateTime object
                string sdate = Encoding.UTF8.GetString(propItem.Value).Trim();
                string secondhalf = sdate.Substring(sdate.IndexOf(" "), (sdate.Length - sdate.IndexOf(" ")));
                string firsthalf = sdate.Substring(0, 10);
                firsthalf = firsthalf.Replace(":", "-");
                sdate = firsthalf + secondhalf;
                dtaken = DateTime.Parse(sdate);
                return dtaken;
            }
            catch (Exception e)
            {
                log.Error("Cannot find property for image: " + fileinfo.FullName, e);
                return DateTime.MinValue;
            }
        }

        public static void DirTraverse(DirectoryInfo dir, Action<FileInfo> action)
        {
            log.InfoFormat("Current traversing folder: {0}", dir.FullName);
            try
            {
                foreach (FileInfo fi in dir.GetFiles())
                {
                    try
                    {
                        action(fi);
                    }
                    catch (System.Exception exception)
                    {
                        log.Error("Exception when trying to rename the image: " + fi.FullName, exception);
                    }
                }

                foreach (DirectoryInfo d in dir.GetDirectories())
                {
                    DirTraverse(d, action);
                }
            }
            catch (System.Exception excpt)
            {
                log.Error("Exception when traversing directories, current folder: " + dir.FullName, excpt);
            }
        }
    }
}
