using System.Globalization;
using RenameImages;
using Xunit;

namespace RenameImages.Tests;

public class ProgramTests
{
    [Theory]
    [InlineData("20240102_030405.jpg", 2024, 1, 2, 3, 4, 5, 0)]
    [InlineData("20240102_030405123_camera.heic", 2024, 1, 2, 3, 4, 5, 123)]
    public void TryParseDateTakenFromFileName_ParsesSupportedPrefixes(
        string fileName,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second,
        int millisecond)
    {
        bool parsed = Program.TryParseDateTakenFromFileName(fileName, out DateTime dateTaken);

        Assert.True(parsed);
        Assert.Equal(new DateTime(year, month, day, hour, minute, second, millisecond), dateTaken);
    }

    [Theory]
    [InlineData("20241302_030405.jpg")]
    [InlineData("photo.jpg")]
    public void TryParseDateTakenFromFileName_ReturnsFalseForInvalidPrefixes(string fileName)
    {
        bool parsed = Program.TryParseDateTakenFromFileName(fileName, out DateTime dateTaken);

        Assert.False(parsed);
        Assert.Equal(DateTime.MinValue, dateTaken);
    }

    [Fact]
    public void RenameFile_UsesNextAvailableNameWhenTimestampNamesAlreadyExist()
    {
        using var directory = new TemporaryDirectory();
        var file = new FileInfo(Path.Combine(directory.Path, "source.jpg"));
        string sourcePath = file.FullName;
        File.WriteAllText(file.FullName, "source");
        File.WriteAllText(Path.Combine(directory.Path, "2024-01-02 03.04.05.jpg"), "existing");
        File.WriteAllText(Path.Combine(directory.Path, "2024-01-02 03.04.05-1.jpg"), "existing");

        Program.RenameFile(file, "yyyy-MM-dd HH.mm.ss", new DateTime(2024, 1, 2, 3, 4, 5));

        string renamedFile = Path.Combine(directory.Path, "2024-01-02 03.04.05-2.jpg");
        Assert.False(File.Exists(sourcePath));
        Assert.Equal("source", File.ReadAllText(renamedFile));
        Assert.Equal("existing", File.ReadAllText(Path.Combine(directory.Path, "2024-01-02 03.04.05.jpg")));
    }

    [Fact]
    public void DirTraverse_VisitsFilesInRootAndNestedDirectories()
    {
        using var directory = new TemporaryDirectory();
        string nestedPath = Directory.CreateDirectory(Path.Combine(directory.Path, "nested", "deeper")).FullName;
        string[] expectedFiles =
        [
            Path.Combine(directory.Path, "root.jpg"),
            Path.Combine(directory.Path, "nested", "nested.jpg"),
            Path.Combine(nestedPath, "deep.jpg")
        ];

        foreach (string filePath in expectedFiles)
        {
            File.WriteAllText(filePath, string.Empty);
        }

        var visitedFiles = new List<string>();
        Program.DirTraverse(new DirectoryInfo(directory.Path), file => visitedFiles.Add(file.FullName));

        Assert.Equal(expectedFiles.Order(), visitedFiles.Order());
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
