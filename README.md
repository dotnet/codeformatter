# deadregions
Tool for analyzing and cleaning up unecessary ("dead") conditional regions in C# code
```
SYNTAX
  DeadRegions [<project> ...] [options]
  DeadRegions [<source file> ...] [options]

OPTIONS
  config <symbol list>
    Specify a complete symbol configuration
    [multiple specifications allowed]

  ignore <symbol list>
    Ignore a list of symbols (treat as varying)
    [multiple specifications allowed]

  define <symbol list>
    Define a list of symbols (treat as always true)
    [multiple specifications allowed]

  disable <symbol list>
    Disable a list of symbols (treat as always disabled)
    [multiple specifications allowed]

  default <false|true|varying>
    Set the default value for symbols which do not have a specified value (defaults
    to varying)

  printdisabled
    Print the list of always disabled conditional regions

  printenabled
    Print the list of always enabled conditional regions

  printvarying
    Print the list of varying conditional regions

  printsymbols
    Print the lists of uniquely specified preprocessor symbols, symbols visited
    during analysis, and symbols not encountered during analysis

  print
    Print the entire list of conditional regions and the lists of preprocessor
    symbols (combination of printenabled, printdisabled, printvarying, and
    printsymbols)

  edit
    Perform edits to remove always enabled and always disabled conditional regions
    from source files, and simplify preprocessor expressions which evaluate to
    'varying'


NOTES
  <symbol list> is a comma or semi-colon separated list of preprocessor symbols
```

An unnecessary conditional region is one conditioned on a preprocessor expression that, across all possible build configurations for a given project, always evaluates to `true` or always evaluates to `false`. Such regions are either dead code (if conditioned on `false`) or have unnecessary preprocessor directives (if conditioned on `true`). Conversely, regions conditioned on preprocessor expressions which evaluate differently across different build configurations have meaningful preprocessor directives.

This tool can analyze a given project to determine which branching preprocessor directives and regions are unnecessary, and optionally remove them.

# Examples

## Analyze all conditional regions in a project

`> deadregions example.csproj /print`

This will print out something like
```
D:\example\Program.cs(2): "#if true" : Always Enabled
D:\example\Program.cs(4): "#else" : Always Disabled
D:\example\Program.cs(8): "#if ZERO // TODO(somebody): Re-enable this when x is fixed" : Varying
D:\example\Program.cs(14): "#if DEBUG" : Varying
Conditional Regions
      4 found in total
      1 always disabled
      1 always enabled
      2 varying

Symbols
      0 unique symbol(s) specified:
      3 unique symbol(s) visited: true;ZERO;DEBUG
      0 specified symbol(s) unvisited:
```

There are a few things going on here. As you would expect, the `#if true` region is determined to be always enabled, and the corresponding `#else` is always disabled.  You'll also notice that you get a summary of information about the conditional regions found in the project, as well as the preprocessor symbols that were specified on the command line, found in the project.

One interesting thing to note is that the `#if ZERO` and `#if DEBUG` regions are determined to be varying. That is because unlike the C# preprocessor, this tool evaluates symbols which do not have a specified value as "varying" by default. The rational behind this is that most of the time, you'll be using this tool to remove dead conditional regions, but you won't necessarily specify or even have all the data about all possible build configurations. So in order to avoid removing regions which are determined to be always disabled simply because you didn't specify a value for a symbol, the tool defaults the value to varying, which causes the region to be ignored in the clean-up pass.

If you would like to get the same behavior as the C# preprocessor (default undefined symbols to `false`), just pass `/default false`

```
> deadregions .\example.csproj /print /default false
D:\example\Program.cs(2): "#if true" : Always Enabled
D:\example\Program.cs(4): "#else" : Always Disabled
D:\example\Program.cs(8): "#if ZERO // TODO(somebody): Re-enable this when x is fixed" : Always Disabled
D:\example\Program.cs(14): "#if DEBUG" : Always Disabled
```

Voilà.

You can also provide specific values for preprocessor expressions using `/define`, `/disable` and `/ignore`. For example, 
```
> deadregions .\example.csproj /print /disable ZERO
...
D:\example\Program.cs(8): "#if ZERO // TODO(somebody): Re-enable this when x is fixed" : Always Disabled
...
```

## Remove all unnecessary regions in a project

When you're ready to make edits to your source files based on the output of analysis (or you're using version control and you'd like to hurry up and produce a diff already), pass `/edit`

`> deadregions.exe example.csproj /disable ZERO /edit`

In my example, if I analyze the project again, I'm only left with varying regions as expected.

```
> deadregions .\example.csproj /print
D:\example\Program.cs(5): "#if DEBUG" : Varying
Conditional Regions
      1 found in total
      1 varying

Symbols
      0 unique symbol(s) specified:
      1 unique symbol(s) visited: DEBUG
      0 specified symbol(s) unvisited:
```

## Remove all unnecessary regions conditioned with "#if false"

Since you can override how literal preprocessor expressions evaluate, you can also use a similar command to remove all unnecessary regions conditioned with "#if false"

`> deadregions example.csproj /disable false /edit`

## Analyze a project with many different build configurations

If you're working with a large codebase with many different build configurations (and sets of preprocessor symbols), chances are you won't have an easy time figuring out or specifying the values for all those symbols by hand. Enter the `/config` switch: you can use this switch to specify each of your build configurations in their entirety, and the tool will evaluate each preprocessor expression in the context of each build configuration to determine the state of each conditional region.

`> deadregions hugecodebase.csproj /config A;B;C /config A;D;E;F;DEBUG /config EXPENSIVE_LOGGING;D;E`

By combining this with explicit specification of symbol values (maybe you have some build configurations which are only run by certain parts of a larger team so you don't know the whole of those configurations but you know the symbols involved), you should be able to pinpoint the set of symbols and conditional regions you care about.

## Use a response file to avoid a long command line

Since such command lines can get relatively long, it may be useful to specify options using a *response file*.

`> deadregions hugecodebase.csproj @hugecodebase.rsp`

where the file `hugecodebase.rsp` contains

`/config A;B;C /config A;D;E;F;DEBUG /config EXPENSIVE_LOGGING;D;E`

etc.
