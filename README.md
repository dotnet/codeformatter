# CodeFormatter

***NOTE: This repo is archived. The replacement is [dotnet/format](https://github.com/dotnet/format).***

CodeFormatter is a tool that uses Roslyn to automatically rewrite the source to
follow our coding styles, which are [documented here][dotnet-coding-style].

[dotnet-coding-style]: https://github.com/dotnet/runtime/blob/master/docs/coding-guidelines/coding-style.md

## Prerequisites

In order to build or run this tool you will need to have Microsoft Build Tools
2015 installed.  This comes as a part of [Visual Studio 2015](https://www.visualstudio.com/downloads/download-visual-studio-vs).

## Installation

Download binaries from [GitHub Releases](https://github.com/dotnet/codeformatter/releases)

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

We follow the same contribution process that the
[dotnet runtime is using][dotnet-contributing].

[dotnet-contributing]: https://github.com/dotnet/runtime/blob/master/CONTRIBUTING.md
