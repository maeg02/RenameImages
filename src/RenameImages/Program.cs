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
                string dirPath = GetDirectoryPathFromArgs(args);

                log.DebugFormat("Working direcory is: {0}", dirPath);

                DirectoryInfo dir = new DirectoryInfo(dirPath);
                log.Info("Proccessing files");

                CultureInfo provider = CultureInfo.InvariantCulture;
                const string dateFormats = "yyyy-MM-dd HH.mm.ss";

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

        private static string GetDirectoryPathFromArgs(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return Environment.CurrentDirectory;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string current = args[i];

                if (string.Equals(current, "-p", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(current, "--path", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        return args[i + 1];
                    }

                    throw new ArgumentException("Missing value for -p/--path option.");
                }
            }

            string firstArg = args[0];
            if (firstArg.StartsWith("-p=", StringComparison.OrdinalIgnoreCase) ||
                firstArg.StartsWith("--path=", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = firstArg.Split('=', 2);
                if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    return parts[1];
                }

                throw new ArgumentException("Missing value for -p/--path option.");
            }

            return firstArg;
        }

        internal static void RenameFile(FileInfo file, string dateFormats, DateTime dateTaken)
        {
            if (dateTaken == DateTime.MinValue)
            {
                return;
            }

            string directoryPath = file.DirectoryName ?? string.Empty;
            string baseName = dateTaken.ToString(dateFormats, CultureInfo.InvariantCulture);
            string suggestedName = Path.Combine(directoryPath, baseName + file.Extension);
            int index = 1;

            while (File.Exists(suggestedName))
            {
                suggestedName = Path.Combine(directoryPath, baseName + "-" + index + file.Extension);
                index++;
            }

            file.MoveTo(suggestedName);
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

                log.WarnFormat("No supported capture date metadata found: {0}, guessing on file name", fileinfo.FullName);

                if (TryParseDateTakenFromFileName(fileinfo.Name, out dateTaken))
                {
                    return dateTaken;
                }

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

        internal static bool TryParseDateTakenFromFileName(string fileName, out DateTime dateTaken)
        {
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);

            if (fileNameWithoutExtension.Length >= 18)
            {
                var datePrefixWithMilliseconds = fileNameWithoutExtension.Substring(0, 18);
                if (DateTime.TryParseExact(datePrefixWithMilliseconds, "yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTaken))
                {
                    return true;
                }
            }

            if (fileNameWithoutExtension.Length >= 15)
            {
                var datePrefix = fileNameWithoutExtension.Substring(0, 15);
                if (DateTime.TryParseExact(datePrefix, "yyyyMMdd_HHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTaken))
                {
                    return true;
                }
            }

            dateTaken = DateTime.MinValue;
            return false;
        }

        public static void DirTraverse(DirectoryInfo dir, Action<FileInfo> action)
        {
            log.InfoFormat("Current traversing folder: {0}", dir.ToString());
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
                log.Error("Exception when traversing directories, current folder: " + dir.ToString(), excpt);
            }
        }
    }
}
