# CodeFormatter

[![Build status](http://dotnet-ci.cloudapp.net/job/dotnet_codeformatter/badge/icon)](http://dotnet-ci.cloudapp.net/job/dotnet_codeformatter/)

CodeFormatter is a tool that uses Roslyn to automatically rewrite the source to
follow our coding styles, which are [documented here][corefx-coding-style].

[corefx-coding-style]: https://github.com/dotnet/corefx/wiki/Contributing#c-coding-style

## Prerequisites

In order to build or run this tool you will need to have Microsoft Build Tools
2015 Preview installed.  This comes as a part of Visual Studio 2015 Preview or
can be installed separately from [here][vs-2015-download].

[vs-2015-download]: http://www.visualstudio.com/en-us/downloads/visual-studio-2015-downloads-vs

## Usage

In order get the usage, simply invoke the tool with no arguments:

```
$ .\CodeFormatter.exe
CodeFormatter <project or solution> [<rule types>] [/file:<filename>] [/nocopyright] [/c:<config1,config2> [/copyright:file]
    <rule types> - Rule types to use in addition to the default ones.
                   Use ConvertTests to convert MSTest tests to xUnit.
    <filename>   - Only apply changes to files with specified name.
    <configs>    - Additional preprocessor configurations the formatter
                   should run under.
    <copyright>  - Specifies file containing copyright header.
```

## Contributing

We follow the same contribution process that 
[corefx is using][corefx-contributing].

[corefx-contributing]: https://github.com/dotnet/corefx/wiki/Contributing
