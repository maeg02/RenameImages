using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RenameImages
{
    public class CommandLineOptions
    {
        [Option('p')]
        public string Path { get; set; }
    }
}
