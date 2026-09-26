using CommandLine;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

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

                string[] supportedImageFileExtensions = new string[] { ".jpg", ".jpeg", ".heic", ".heif" };


                Action<FileInfo> renameFileAction = file =>
                {
                    if (!supportedImageFileExtensions.Contains(file.Extension.ToLower()))
                        return;

                    string name = file.Name.Replace(file.Extension, "");

                    DateTime dateTaken;

                    // check if the pattern is valid for this file
                    if (DateTime.TryParseExact(name, dateFormats, provider, DateTimeStyles.AllowWhiteSpaces, out dateTaken))
                        return;


                    dateTaken = GetDateTaken(file);

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

        public static DateTime GetDateTaken(FileInfo fileinfo)
        {
            try
            {
                IReadOnlyList<MetadataExtractor.Directory> directories = ImageMetadataReader.ReadMetadata(fileinfo.FullName);

                DateTime dateTaken;

                ExifSubIfdDirectory exifSubIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exifSubIfdDirectory != null &&
                    exifSubIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out dateTaken))
                {
                    return dateTaken;
                }

                ExifIfd0Directory exifIfd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (exifIfd0Directory != null &&
                    exifIfd0Directory.TryGetDateTime(ExifDirectoryBase.TagDateTime, out dateTaken))
                {
                    return dateTaken;
                }

                log.WarnFormat("No supported capture date metadata found: {0}", fileinfo.FullName);
                return DateTime.MinValue;
            }
            catch (ImageProcessingException exception)
            {
                log.WarnFormat("Not able to read metadata from image: {0}. {1}", fileinfo.FullName, exception.Message);
                return DateTime.MinValue;
            }
            catch (FileNotFoundException)
            {
                log.WarnFormat("File no longer present: {0}", fileinfo.FullName);
                return DateTime.MinValue;
            }
            catch (Exception exception)
            {
                log.Error("Cannot read date metadata for image: " + fileinfo.FullName, exception);
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
