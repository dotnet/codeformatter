using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DeadRegionAnalysis
{
    public interface IAnalysisLogger
    {
        void Write(string format, params object[] args);
        void WriteLine(string format, params object[] args);
        void WriteLine();
        void WriteErrorLine(string format, params object[] args);
    }

    /// <summary>
    /// This implementation will forward all output directly to the console.
    /// </summary>
    internal sealed class ConsoleAnalysisLogger : IAnalysisLogger
    {
        public void Write(string format, params object[] args)
        {
            Console.Write(format, args);
        }

        public void WriteLine(string format, params object[] args)
        {
            Console.WriteLine(format, args);
        }

        public void WriteErrorLine(string format, params object[] args)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("error: ");
            Console.WriteLine(format, args);
            Console.ResetColor();
        }

        public void WriteLine()
        {
            Console.WriteLine();
        }
    }

    /// <summary>
    /// This implementation just ignores all output from the formatter.  It's useful
    /// for unit testing purposes.
    /// </summary>
    internal sealed class EmptyFormatLogger : IAnalysisLogger
    {
        public void Write(string format, params object[] args)
        {
        }

        public void WriteLine(string format, params object[] args)
        {
        }

        public void WriteErrorLine(string format, params object[] argsa)
        {
        }

        public void WriteLine()
        {
        }
    }
}
