using System.Diagnostics;

namespace CodeFormatter
{
    public sealed class CommandLineParseResult
    {
        private readonly CommandLineOptions _options;
        private readonly string _error;

        public bool IsSuccess
        {
            get { return _options != null; }
        }

        public bool IsError
        {
            get { return !IsSuccess; }
        }

        public CommandLineOptions Options
        {
            get
            {
                Debug.Assert(IsSuccess);
                return _options;
            }
        }

        public string Error
        {
            get
            {
                Debug.Assert(IsError);
                return _error;
            }
        }

        private CommandLineParseResult(CommandLineOptions options = null, string error = null)
        {
            _options = options;
            _error = error;
        }

        public static CommandLineParseResult CreateSuccess(CommandLineOptions options)
        {
            return new CommandLineParseResult(options: options);
        }

        public static CommandLineParseResult CreateError(string error)
        {
            return new CommandLineParseResult(error: error);
        }
    }
}
